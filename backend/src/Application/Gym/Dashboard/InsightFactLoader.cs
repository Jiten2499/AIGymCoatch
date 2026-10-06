using LifeFitAI.Application.Gym;
using LifeFitAI.SharedKernel.Abstractions;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed class InsightFactLoader
{
    private readonly IDashboardAnalytics _analytics;
    private readonly IDashboardQuery _kpis;
    private readonly IDateTimeProvider _clock;

    public InsightFactLoader(IDashboardAnalytics analytics, IDashboardQuery kpis, IDateTimeProvider clock)
    {
        _analytics = analytics;
        _kpis = kpis;
        _clock = clock;
    }

    public async Task<BusinessInsightFacts> LoadAsync(DashboardWorkspace workspace, ResolvedAnalytics current, bool members, bool attendance, bool memberships, CancellationToken cancellationToken)
    {
        var previousWindow = InsightMath.Previous(current.From, current.To);
        var previous = ResolvedAnalytics.Resolve(previousWindow.From, previousWindow.To, current.Period, current.TimeZone);
        var tenantId = workspace.Tenant.Id;
        MetricChange? newMembers = null;
        MetricChange? checkIns = null;
        decimal? average = null;
        MetricChange? created = null;
        MetricChange? renewed = null;
        MetricChange? expired = null;

        if (members)
        {
            newMembers = InsightMath.Compare(await Joins(tenantId, current, cancellationToken).ConfigureAwait(false), previous.IsSuccess ? await Joins(tenantId, previous.Value, cancellationToken).ConfigureAwait(false) : null);
        }

        if (attendance)
        {
            var currentTotal = await CheckIns(tenantId, current, cancellationToken).ConfigureAwait(false);
            checkIns = InsightMath.Compare(currentTotal, previous.IsSuccess ? await CheckIns(tenantId, previous.Value, cancellationToken).ConfigureAwait(false) : null);
            average = InsightMath.AverageDaily(currentTotal, current.To.DayNumber - current.From.DayNumber + 1);
        }

        if (memberships)
        {
            var currentActivity = await _analytics.CountMembershipActivityAsync(tenantId, current.From, current.To, cancellationToken).ConfigureAwait(false);
            var previousActivity = previous.IsSuccess
                ? await _analytics.CountMembershipActivityAsync(tenantId, previous.Value.From, previous.Value.To, cancellationToken).ConfigureAwait(false)
                : null;
            created = InsightMath.Compare(Total(current, currentActivity, "Created"), previous.IsSuccess ? Total(previous.Value, previousActivity!, "Created") : null);
            renewed = InsightMath.Compare(Total(current, currentActivity, "Renewed"), previous.IsSuccess ? Total(previous.Value, previousActivity!, "Renewed") : null);
            expired = InsightMath.Compare(Total(current, currentActivity, "Expired"), previous.IsSuccess ? Total(previous.Value, previousActivity!, "Expired") : null);
        }

        var counts = members || attendance || memberships
            ? await _kpis.CountAsync(tenantId, _clock.UtcNow, workspace.TimeZone, new KpiScope(members, attendance, memberships, false), cancellationToken).ConfigureAwait(false)
            : null;

        return new BusinessInsightFacts(
            InsightMath.Window(current.From, current.To),
            previous.IsSuccess ? previousWindow : null,
            members ? counts?.TotalMembers : null,
            members ? counts?.ActiveMembers : null,
            newMembers,
            checkIns,
            average,
            attendance ? counts?.CheckInsToday : null,
            memberships ? counts?.ActiveMemberships : null,
            memberships ? counts?.ExpiringIn7Days : null,
            created,
            renewed,
            expired);
    }

    private async Task<int> Joins(Guid tenantId, ResolvedAnalytics calendar, CancellationToken cancellationToken)
    {
        var counts = await _analytics.CountMemberJoinsAsync(tenantId, calendar.From, calendar.To, cancellationToken).ConfigureAwait(false);
        return InsightMath.Sum(calendar, counts, "newMembers");
    }

    private async Task<int> CheckIns(Guid tenantId, ResolvedAnalytics calendar, CancellationToken cancellationToken)
    {
        var counts = await _analytics.CountCheckInsByLocalDateAsync(tenantId, calendar.FromUtc, calendar.ToUtc, calendar.TimeZone, cancellationToken).ConfigureAwait(false);
        return InsightMath.Sum(calendar, counts, "checkIns");
    }

    private static int Total(ResolvedAnalytics calendar, IReadOnlyList<MembershipActivityCount> counts, string action) =>
        InsightMath.Sum(calendar, counts.Where(count => count.Action == action).Select(count => new DateCount(count.Date, count.Count)).ToArray(), action);
}
