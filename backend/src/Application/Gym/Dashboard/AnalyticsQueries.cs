using FluentValidation;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record GetMemberGrowthAnalyticsQuery(DateOnly From, DateOnly To, AnalyticsPeriod Period) : IRequest<Result<AnalyticsReport>>;

public sealed record GetAttendanceAnalyticsQuery(DateOnly From, DateOnly To, AnalyticsPeriod Period) : IRequest<Result<AnalyticsReport>>;

public sealed record GetMembershipAnalyticsQuery(DateOnly From, DateOnly To, AnalyticsPeriod Period) : IRequest<Result<AnalyticsReport>>;

public sealed class GetMemberGrowthAnalyticsQueryValidator : AbstractValidator<GetMemberGrowthAnalyticsQuery>
{
    public GetMemberGrowthAnalyticsQueryValidator() => AnalyticsWindowRules.Apply(this, query => query.From, query => query.To, query => query.Period);
}

public sealed class GetAttendanceAnalyticsQueryValidator : AbstractValidator<GetAttendanceAnalyticsQuery>
{
    public GetAttendanceAnalyticsQueryValidator() => AnalyticsWindowRules.Apply(this, query => query.From, query => query.To, query => query.Period);
}

public sealed class GetMembershipAnalyticsQueryValidator : AbstractValidator<GetMembershipAnalyticsQuery>
{
    public GetMembershipAnalyticsQueryValidator() => AnalyticsWindowRules.Apply(this, query => query.From, query => query.To, query => query.Period);
}

internal static class AnalyticsWindowRules
{
    public static void Apply<T>(AbstractValidator<T> validator, Func<T, DateOnly> from, Func<T, DateOnly> to, Func<T, AnalyticsPeriod> period)
    {
        validator.RuleFor(item => item).Must(item => ResolvedAnalytics.Resolve(from(item), to(item), period(item), TimeZoneInfo.Utc).IsSuccess)
            .WithMessage("The analytics range is not valid for this period.");
    }
}

public sealed class GetMemberGrowthAnalyticsQueryHandler : IRequestHandler<GetMemberGrowthAnalyticsQuery, Result<AnalyticsReport>>
{
    private readonly IDashboardAccess _access;
    private readonly IDashboardAnalytics _analytics;

    public GetMemberGrowthAnalyticsQueryHandler(IDashboardAccess access, IDashboardAnalytics analytics)
    {
        _access = access;
        _analytics = analytics;
    }

    public Task<Result<AnalyticsReport>> Handle(GetMemberGrowthAnalyticsQuery request, CancellationToken cancellationToken) =>
        AnalyticsExecution.Run(_access, "Members", request.From, request.To, request.Period, async (workspace, calendar, token) =>
        {
            var counts = await _analytics.CountMemberJoinsAsync(workspace.Tenant.Id, calendar.From, calendar.To, token).ConfigureAwait(false);
            return [calendar.Series("newMembers", "New members", calendar.Rollup(counts))];
        }, cancellationToken);
}

public sealed class GetAttendanceAnalyticsQueryHandler : IRequestHandler<GetAttendanceAnalyticsQuery, Result<AnalyticsReport>>
{
    private readonly IDashboardAccess _access;
    private readonly IDashboardAnalytics _analytics;

    public GetAttendanceAnalyticsQueryHandler(IDashboardAccess access, IDashboardAnalytics analytics)
    {
        _access = access;
        _analytics = analytics;
    }

    public Task<Result<AnalyticsReport>> Handle(GetAttendanceAnalyticsQuery request, CancellationToken cancellationToken) =>
        AnalyticsExecution.Run(_access, "Attendance", request.From, request.To, request.Period, async (workspace, calendar, token) =>
        {
            var counts = await _analytics.CountCheckInsByLocalDateAsync(workspace.Tenant.Id, calendar.FromUtc, calendar.ToUtc, calendar.TimeZone, token).ConfigureAwait(false);
            return [calendar.Series("checkIns", "Check-ins", calendar.Rollup(counts))];
        }, cancellationToken);
}

public sealed class GetMembershipAnalyticsQueryHandler : IRequestHandler<GetMembershipAnalyticsQuery, Result<AnalyticsReport>>
{
    private readonly IDashboardAccess _access;
    private readonly IDashboardAnalytics _analytics;

    public GetMembershipAnalyticsQueryHandler(IDashboardAccess access, IDashboardAnalytics analytics)
    {
        _access = access;
        _analytics = analytics;
    }

    public Task<Result<AnalyticsReport>> Handle(GetMembershipAnalyticsQuery request, CancellationToken cancellationToken) =>
        AnalyticsExecution.Run(_access, "Memberships", request.From, request.To, request.Period, async (workspace, calendar, token) =>
        {
            var counts = await _analytics.CountMembershipActivityAsync(workspace.Tenant.Id, calendar.From, calendar.To, token).ConfigureAwait(false);
            return
            [
                calendar.Series("newMemberships", "New memberships", calendar.Rollup(Filter(counts, "Created"))),
                calendar.Series("renewedMemberships", "Renewals", calendar.Rollup(Filter(counts, "Renewed"))),
                calendar.Series("expiredMemberships", "Expired memberships", calendar.Rollup(Filter(counts, "Expired"))),
            ];
        }, cancellationToken);

    private static IEnumerable<DateCount> Filter(IReadOnlyList<MembershipActivityCount> counts, string action) =>
        counts.Where(count => count.Action == action).Select(count => new DateCount(count.Date, count.Count));
}

internal static class AnalyticsExecution
{
    public static async Task<Result<AnalyticsReport>> Run(
        IDashboardAccess access,
        string area,
        DateOnly from,
        DateOnly to,
        AnalyticsPeriod period,
        Func<DashboardWorkspace, ResolvedAnalytics, CancellationToken, Task<IReadOnlyList<AnalyticsSeries>>> load,
        CancellationToken cancellationToken)
    {
        var opened = await access.OpenAreaAsync(area, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<AnalyticsReport>(opened.Error);
        }

        var calendar = ResolvedAnalytics.Resolve(from, to, period, opened.Value.TimeZone);
        if (calendar.IsFailure)
        {
            return Result.Failure<AnalyticsReport>(calendar.Error);
        }

        var series = await load(opened.Value, calendar.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new AnalyticsReport(calendar.Value.From, calendar.Value.To, calendar.Value.Period, series));
    }
}
