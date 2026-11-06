using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Dashboard;

public readonly record struct DateAmount(DateOnly Date, decimal Amount);

public sealed record RevenuePoint(DateOnly PeriodStart, DateOnly PeriodEnd, string Label, decimal Amount);

public sealed record DashboardSession(Guid Id, string MemberName, DateTime StartUtc);

public sealed record DashboardActivity(string Kind, string Title, DateTime AtUtc);

public sealed record DashboardAlert(string Kind, string Title, int Count);

public sealed record DashboardBoard(
    DateOnly From,
    DateOnly To,
    string Currency,
    int? ActiveMembers,
    decimal? PaymentsCollected,
    int? CheckInsToday,
    int? ExpiringSoon,
    int? InactiveMembers,
    int? OverduePayments,
    int? LeadsToFollowUp,
    IReadOnlyList<RevenuePoint>? Revenue,
    IReadOnlyList<DashboardSession>? Sessions,
    IReadOnlyList<DashboardActivity>? Activities,
    IReadOnlyList<DashboardAlert>? Alerts);

public interface IDashboardBoard
{
    Task<IReadOnlyList<DateAmount>> PostedPaymentsAsync(Guid tenantId, string currency, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<int> CountInactiveMembersAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<int> CountOverdueMembershipsAsync(Guid tenantId, DateOnly today, CancellationToken cancellationToken);

    Task<int> CountFollowUpsAsync(Guid tenantId, DateTime beforeUtc, CancellationToken cancellationToken);

    Task<int> CountExpiringOnAsync(Guid tenantId, DateOnly day, CancellationToken cancellationToken);

    Task<IReadOnlyList<DashboardSession>> UpcomingSessionsAsync(Guid tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<DashboardActivity>> ActivitiesAsync(Guid tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

public static class RevenueSeries
{
    public static IReadOnlyList<RevenuePoint> Rollup(ResolvedAnalytics calendar, IReadOnlyList<DateAmount> days)
    {
        var totals = new Dictionary<DateOnly, decimal>();
        foreach (var day in days)
        {
            if (day.Date < calendar.From || day.Date > calendar.To || day.Amount == 0m)
            {
                continue;
            }

            var start = calendar.BucketStart(day.Date);
            totals[start] = totals.GetValueOrDefault(start) + day.Amount;
        }

        return calendar.Buckets.Select(bucket => new RevenuePoint(bucket.Start, bucket.End, bucket.Label, totals.GetValueOrDefault(bucket.Start))).ToArray();
    }

    public static decimal Collected(IReadOnlyList<RevenuePoint> points) => points.Sum(point => point.Amount);
}

public sealed record GetDashboardBoardQuery(DateOnly From, DateOnly To, AnalyticsPeriod Period) : IRequest<Result<DashboardBoard>>;

public sealed class GetDashboardBoardQueryValidator : AbstractValidator<GetDashboardBoardQuery>
{
    public GetDashboardBoardQueryValidator() => AnalyticsWindowRules.Apply(this, query => query.From, query => query.To, query => query.Period);
}

public sealed class GetDashboardBoardQueryHandler : IRequestHandler<GetDashboardBoardQuery, Result<DashboardBoard>>
{
    private readonly IDashboardAccess _access;
    private readonly IFeatureFlagService _features;
    private readonly IDashboardQuery _kpis;
    private readonly IDashboardBoard _board;
    private readonly IDateTimeProvider _clock;

    public GetDashboardBoardQueryHandler(IDashboardAccess access, IFeatureFlagService features, IDashboardQuery kpis, IDashboardBoard board, IDateTimeProvider clock)
    {
        _access = access;
        _features = features;
        _kpis = kpis;
        _board = board;
        _clock = clock;
    }

    public async Task<Result<DashboardBoard>> Handle(GetDashboardBoardQuery request, CancellationToken cancellationToken)
    {
        var opened = await _access.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<DashboardBoard>(opened.Error);
        }

        var calendar = ResolvedAnalytics.Resolve(request.From, request.To, request.Period, opened.Value.TimeZone);
        if (calendar.IsFailure)
        {
            return Result.Failure<DashboardBoard>(calendar.Error);
        }

        var workspace = opened.Value;
        var members = await Allowed(workspace, PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        var attendance = await Allowed(workspace, PermissionCodes.AttendanceRead, FeatureCodes.Attendance, cancellationToken).ConfigureAwait(false);
        var memberships = await Allowed(workspace, PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        var payments = await Allowed(workspace, PermissionCodes.PaymentRead, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
        var leads = await Allowed(workspace, PermissionCodes.CrmRead, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        var counts = await _kpis.CountAsync(workspace.Tenant.Id, _clock.UtcNow, workspace.TimeZone, new KpiScope(members, attendance, memberships, false), cancellationToken).ConfigureAwait(false);
        var currency = string.IsNullOrWhiteSpace(workspace.Profile?.Currency) ? "USD" : workspace.Profile!.Currency;
        var revenue = payments ? RevenueSeries.Rollup(calendar.Value, await _board.PostedPaymentsAsync(workspace.Tenant.Id, currency, calendar.Value.From, calendar.Value.To, cancellationToken).ConfigureAwait(false)) : null;
        var today = DateOnly.FromDateTime(workspace.LocalNow);
        var inactive = members ? await _board.CountInactiveMembersAsync(workspace.Tenant.Id, cancellationToken).ConfigureAwait(false) : (int?)null;
        var overdue = payments ? await _board.CountOverdueMembershipsAsync(workspace.Tenant.Id, today, cancellationToken).ConfigureAwait(false) : (int?)null;
        var followUps = leads ? await _board.CountFollowUpsAsync(workspace.Tenant.Id, Tomorrow(workspace), cancellationToken).ConfigureAwait(false) : (int?)null;
        var sessions = memberships ? await _board.UpcomingSessionsAsync(workspace.Tenant.Id, TodayStart(workspace), Tomorrow(workspace), cancellationToken).ConfigureAwait(false) : null;
        var activities = leads ? await _board.ActivitiesAsync(workspace.Tenant.Id, TodayStart(workspace), Tomorrow(workspace), cancellationToken).ConfigureAwait(false) : null;
        var alerts = await Alerts(workspace, memberships, leads, today, cancellationToken).ConfigureAwait(false);
        return Result.Success(new DashboardBoard(
            calendar.Value.From,
            calendar.Value.To,
            currency,
            members ? counts.ActiveMembers : null,
            payments ? RevenueSeries.Collected(revenue!) : null,
            attendance ? counts.CheckInsToday : null,
            memberships ? counts.ExpiringIn7Days : null,
            inactive,
            overdue,
            followUps,
            revenue,
            sessions,
            activities,
            alerts));
    }

    private async Task<IReadOnlyList<DashboardAlert>?> Alerts(DashboardWorkspace workspace, bool memberships, bool leads, DateOnly today, CancellationToken cancellationToken)
    {
        if (!memberships && !leads)
        {
            return null;
        }

        var alerts = new List<DashboardAlert>();
        if (memberships)
        {
            var expiring = await _board.CountExpiringOnAsync(workspace.Tenant.Id, today, cancellationToken).ConfigureAwait(false);
            if (expiring > 0)
            {
                alerts.Add(new DashboardAlert("expiring-today", "Memberships expire today", expiring));
            }
        }

        if (leads)
        {
            var overdue = await _board.CountFollowUpsAsync(workspace.Tenant.Id, TodayStart(workspace), cancellationToken).ConfigureAwait(false);
            if (overdue > 0)
            {
                alerts.Add(new DashboardAlert("overdue-follow-ups", "Lead follow-ups are overdue", overdue));
            }
        }

        return alerts;
    }

    private async Task<bool> Allowed(DashboardWorkspace workspace, string permission, string feature, CancellationToken cancellationToken) =>
        workspace.Permissions.Contains(permission, StringComparer.Ordinal) && await _features.IsEnabledAsync(workspace.Tenant.Id, feature, cancellationToken).ConfigureAwait(false);

    private static DateTime TodayStart(DashboardWorkspace workspace) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(workspace.LocalNow.Date, DateTimeKind.Unspecified), workspace.TimeZone);

    private static DateTime Tomorrow(DashboardWorkspace workspace) => TodayStart(workspace).AddDays(1);
}
