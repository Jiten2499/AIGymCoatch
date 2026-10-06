using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record GetBusinessDashboardInsightsQuery(DateOnly From, DateOnly To, AnalyticsPeriod Period, bool Refresh) : IRequest<Result<BusinessDashboardInsights>>;

public sealed class GetBusinessDashboardInsightsQueryValidator : AbstractValidator<GetBusinessDashboardInsightsQuery>
{
    public GetBusinessDashboardInsightsQueryValidator() => AnalyticsWindowRules.Apply(this, query => query.From, query => query.To, query => query.Period);
}

public sealed class GetBusinessDashboardInsightsQueryHandler : IRequestHandler<GetBusinessDashboardInsightsQuery, Result<BusinessDashboardInsights>>
{
    private readonly IDashboardAccess _access;
    private readonly IFeatureFlagService _features;
    private readonly InsightFactLoader _facts;
    private readonly IAiProvider _provider;
    private readonly IAiLimits _limits;
    private readonly IAiQuotaService _quota;
    private readonly IAiRepository _usage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IBusinessInsightCache _cache;
    private readonly ILogger<GetBusinessDashboardInsightsQueryHandler> _logger;

    public GetBusinessDashboardInsightsQueryHandler(IDashboardAccess access, IFeatureFlagService features, InsightFactLoader facts, IAiProvider provider, IAiLimits limits, IAiQuotaService quota, IAiRepository usage, IUnitOfWork unitOfWork, IDateTimeProvider clock, IBusinessInsightCache cache, ILogger<GetBusinessDashboardInsightsQueryHandler> logger)
    {
        _access = access;
        _features = features;
        _facts = facts;
        _provider = provider;
        _limits = limits;
        _quota = quota;
        _usage = usage;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<BusinessDashboardInsights>> Handle(GetBusinessDashboardInsightsQuery request, CancellationToken cancellationToken)
    {
        var opened = await _access.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<BusinessDashboardInsights>(opened.Error);
        }

        var calendar = ResolvedAnalytics.Resolve(request.From, request.To, request.Period, opened.Value.TimeZone);
        if (calendar.IsFailure)
        {
            return Result.Failure<BusinessDashboardInsights>(calendar.Error);
        }

        var workspace = opened.Value;
        var members = await Allowed(workspace, PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        var attendance = await Allowed(workspace, PermissionCodes.AttendanceRead, FeatureCodes.Attendance, cancellationToken).ConfigureAwait(false);
        var memberships = await Allowed(workspace, PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        var ai = await Allowed(workspace, PermissionCodes.AiUse, FeatureCodes.AiCoach, cancellationToken).ConfigureAwait(false);
        var facts = await _facts.LoadAsync(workspace, calendar.Value, members, attendance, memberships, cancellationToken).ConfigureAwait(false);
        var attention = AttentionRules.Evaluate(facts);
        if (!ai)
        {
            return Result.Success(new BusinessDashboardInsights(attention, AiInsightSection.Disabled));
        }

        if (!facts.HasActivity())
        {
            return Result.Success(new BusinessDashboardInsights(attention, AiInsightSection.Empty));
        }

        var key = CacheKey(workspace.Tenant.Id, facts);
        if (!request.Refresh && _cache.TryGet(key, out var cached) && cached is not null)
        {
            return Result.Success(new BusinessDashboardInsights(attention, cached));
        }

        if ((await _quota.AllowAsync(workspace.UserId, workspace.Tenant.Id, cancellationToken).ConfigureAwait(false)).IsFailure)
        {
            _logger.LogWarning("Business insight quota blocked for tenant {TenantId}", workspace.Tenant.Id);
            return Result.Success(new BusinessDashboardInsights(attention, AiInsightSection.Unavailable));
        }

        var section = await CompleteAsync(workspace, facts, cancellationToken).ConfigureAwait(false);
        if (section.Status == "Ready")
        {
            _cache.Set(key, section);
        }

        return Result.Success(new BusinessDashboardInsights(attention, section));
    }

    private async Task<AiInsightSection> CompleteAsync(DashboardWorkspace workspace, BusinessInsightFacts facts, CancellationToken cancellationToken)
    {
        var prompt = InsightPrompts.User(facts);
        if (prompt.Length > _limits.MaxPromptCharacters)
        {
            _logger.LogWarning("Business insight prompt exceeded the limit for tenant {TenantId}", workspace.Tenant.Id);
            return AiInsightSection.Unavailable;
        }

        var started = Stopwatch.StartNew();
        var completion = await _provider.CompleteAsync(new AiCompletionRequest(_limits.Model, [new AiPromptMessage("system", InsightPrompts.System), new AiPromptMessage("user", prompt)], Math.Min(_limits.MaxTokens, 600)), cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Business insight {Outcome} for tenant {TenantId} provider {Provider} model {Model} in {DurationMs} ms", completion.Succeeded ? "succeeded" : "failed", workspace.Tenant.Id, completion.Provider ?? _limits.Provider, completion.Model ?? _limits.Model, started.ElapsedMilliseconds);
        await RecordAsync(workspace, completion, cancellationToken).ConfigureAwait(false);
        return completion.Succeeded ? InsightResponseReader.Read(completion.Content) ?? AiInsightSection.Unavailable : AiInsightSection.Unavailable;
    }

    private async Task RecordAsync(DashboardWorkspace workspace, AiCompletionResult completion, CancellationToken cancellationToken)
    {
        try
        {
            await _usage.AddUsageAsync(AiUsageRecord.Create(Guid.NewGuid(), workspace.UserId, workspace.Tenant.Id, completion.Provider ?? _limits.Provider, completion.Model ?? _limits.Model, AiRequestType.BusinessInsights, completion.InputTokens, completion.OutputTokens, _limits.CostPerThousandTokens, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Business insight usage was not recorded for tenant {TenantId}", workspace.Tenant.Id);
        }
    }

    private async Task<bool> Allowed(DashboardWorkspace workspace, string permission, string feature, CancellationToken cancellationToken) =>
        workspace.Permissions.Contains(permission, StringComparer.Ordinal) && await _features.IsEnabledAsync(workspace.Tenant.Id, feature, cancellationToken).ConfigureAwait(false);

    private static string CacheKey(Guid tenantId, BusinessInsightFacts facts) =>
        $"{InsightPrompts.Version}:{tenantId:N}:{JsonSerializer.Serialize(facts, JsonSerializerOptions.Web)}";
}
