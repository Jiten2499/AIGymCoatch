using System.Linq.Expressions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class DashboardAnalyticsQuery : IDashboardAnalytics
{
    private readonly ApplicationDbContext _db;

    public DashboardAnalyticsQuery(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DateCount>> CountMemberJoinsAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await _db.MemberProfiles.AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.JoinDate >= from && profile.JoinDate <= to)
            .GroupBy(profile => profile.JoinDate)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(row => new DateCount(row.Date, row.Count)).ToArray();
    }

    public async Task<IReadOnlyList<DateCount>> CountCheckInsByLocalDateAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var quarters = await _db.Attendance.AsNoTracking()
            .Where(visit => visit.TenantId == tenantId && visit.CheckInTime >= fromUtc && visit.CheckInTime < toUtc)
            .GroupBy(visit => new
            {
                visit.CheckInTime.Year,
                visit.CheckInTime.Month,
                visit.CheckInTime.Day,
                visit.CheckInTime.Hour,
                Quarter = visit.CheckInTime.Minute / 15,
            })
            .Select(group => new { group.Key.Year, group.Key.Month, group.Key.Day, group.Key.Hour, group.Key.Quarter, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totals = new Dictionary<DateOnly, int>();
        var crossing = new List<(DateTime Start, DateTime End)>();
        foreach (var row in quarters)
        {
            var start = new DateTime(row.Year, row.Month, row.Day, row.Hour, row.Quarter * 15, 0, DateTimeKind.Utc);
            var end = start.AddMinutes(15);
            var firstDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(start, timeZone));
            var lastDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(end.AddTicks(-1), timeZone));
            if (firstDay == lastDay)
            {
                totals[firstDay] = totals.GetValueOrDefault(firstDay) + row.Count;
            }
            else
            {
                crossing.Add((start, end));
            }
        }

        if (crossing.Count > 0)
        {
            var times = await LoadCrossingCheckInsAsync(tenantId, crossing, cancellationToken).ConfigureAwait(false);
            foreach (var time in times)
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(time, DateTimeKind.Utc), timeZone);
                var day = DateOnly.FromDateTime(local);
                totals[day] = totals.GetValueOrDefault(day) + 1;
            }
        }

        return totals.Select(item => new DateCount(item.Key, item.Value)).ToArray();
    }

    public async Task<IReadOnlyList<MembershipActivityCount>> CountMembershipActivityAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await _db.MembershipHistory.AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && entry.EffectiveDate >= from && entry.EffectiveDate <= to
                && (entry.Action == MembershipAction.Created || entry.Action == MembershipAction.Renewed || entry.Action == MembershipAction.Expired))
            .GroupBy(entry => new { entry.EffectiveDate, entry.Action })
            .Select(group => new { group.Key.EffectiveDate, group.Key.Action, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(row => new MembershipActivityCount(row.EffectiveDate, row.Action.ToString(), row.Count)).ToArray();
    }

    public async Task<IReadOnlyList<MembershipSaleCount>> CountMembershipSalesAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = from;
        var end = to;
        var sales = await (
            from history in _db.MembershipHistory.AsNoTracking()
            join membership in _db.GymMemberships.AsNoTracking() on history.GymMembershipId equals membership.Id
            where history.TenantId == tenantId
                && membership.TenantId == tenantId
                && history.EffectiveDate >= start
                && history.EffectiveDate <= end
                && (history.Action == MembershipAction.Created || history.Action == MembershipAction.Renewed)
                && membership.Status != GymMembershipStatus.Cancelled
            select new { membership.Id, membership.MembershipPlanId }).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (sales.Count == 0)
        {
            return [];
        }

        var membershipIds = sales.Select(sale => sale.Id).Distinct().ToArray();
        var payments = await _db.MembershipPayments.AsNoTracking()
            .Where(payment => payment.TenantId == tenantId && membershipIds.Contains(payment.GymMembershipId))
            .Select(payment => new { payment.GymMembershipId, payment.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var voided = payments
            .GroupBy(payment => payment.GymMembershipId)
            .Where(group => group.All(payment => payment.Status == MembershipPaymentStatus.Voided))
            .Select(group => group.Key)
            .ToHashSet();
        var kept = sales.Where(sale => !voided.Contains(sale.Id)).ToArray();
        var planIds = kept.Select(sale => sale.MembershipPlanId).Distinct().ToArray();
        var names = await _db.MembershipPlans.IgnoreQueryFilters().AsNoTracking()
            .Where(plan => plan.TenantId == tenantId && planIds.Contains(plan.Id))
            .Select(plan => new { plan.Id, plan.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var nameById = names.ToDictionary(plan => plan.Id, plan => plan.Name);
        return kept
            .GroupBy(sale => sale.MembershipPlanId)
            .Select(group => new MembershipSaleCount(group.Key, nameById.GetValueOrDefault(group.Key) ?? "Membership", group.Count()))
            .ToArray();
    }

    private async Task<List<DateTime>> LoadCrossingCheckInsAsync(Guid tenantId, List<(DateTime Start, DateTime End)> windows, CancellationToken cancellationToken)
    {
        var visit = Expression.Parameter(typeof(AttendanceRecord), "visit");
        var checkIn = Expression.Property(visit, nameof(AttendanceRecord.CheckInTime));
        Expression body = Expression.Constant(false);
        foreach (var window in windows)
        {
            var match = Expression.AndAlso(
                Expression.GreaterThanOrEqual(checkIn, Expression.Constant(window.Start)),
                Expression.LessThan(checkIn, Expression.Constant(window.End)));
            body = Expression.OrElse(body, match);
        }

        var tenant = Expression.Equal(Expression.Property(visit, nameof(AttendanceRecord.TenantId)), Expression.Constant(tenantId));
        var predicate = Expression.Lambda<Func<AttendanceRecord, bool>>(Expression.AndAlso(tenant, body), visit);
        return await _db.Attendance.AsNoTracking().Where(predicate).Select(item => item.CheckInTime).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
