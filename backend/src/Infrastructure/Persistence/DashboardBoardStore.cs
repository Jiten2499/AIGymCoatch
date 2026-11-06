using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class DashboardBoardStore : IDashboardBoard
{
    private readonly ApplicationDbContext _db;

    public DashboardBoardStore(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DateAmount>> PostedPaymentsAsync(Guid tenantId, string currency, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = from;
        var end = to;
        var rows = await _db.MembershipPayments.AsNoTracking()
            .Where(payment => payment.TenantId == tenantId && payment.Status == MembershipPaymentStatus.Posted && payment.Currency == currency && payment.PaymentDate >= start && payment.PaymentDate <= end)
            .GroupBy(payment => payment.PaymentDate)
            .Select(group => new { Date = group.Key, Amount = group.Sum(payment => payment.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(row => new DateAmount(row.Date, row.Amount)).ToArray();
    }

    public Task<int> CountInactiveMembersAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.MemberProfiles.AsNoTracking().CountAsync(profile => profile.TenantId == tenantId && profile.Status == ProfileStatus.Inactive, cancellationToken);

    public async Task<int> CountOverdueMembershipsAsync(Guid tenantId, DateOnly today, CancellationToken cancellationToken)
    {
        var memberships = await _db.GymMemberships.AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membership.Status != GymMembershipStatus.Cancelled && membership.StartDate <= today && membership.FinalAmount > 0)
            .Select(membership => new { membership.Id, membership.FinalAmount })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (memberships.Count == 0)
        {
            return 0;
        }

        var ids = memberships.Select(membership => membership.Id).ToArray();
        var payments = await _db.MembershipPayments.AsNoTracking()
            .Where(payment => payment.TenantId == tenantId && payment.Status == MembershipPaymentStatus.Posted && ids.Contains(payment.GymMembershipId))
            .Select(payment => new { payment.GymMembershipId, payment.Amount })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var paid = payments.GroupBy(payment => payment.GymMembershipId).ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));
        return memberships.Count(membership => membership.FinalAmount > paid.GetValueOrDefault(membership.Id));
    }

    public Task<int> CountFollowUpsAsync(Guid tenantId, DateTime beforeUtc, CancellationToken cancellationToken) =>
        _db.LeadFollowUps.AsNoTracking().CountAsync(item => item.TenantId == tenantId && item.CompletedAt == null && item.ScheduledAt < beforeUtc, cancellationToken);

    public Task<int> CountExpiringOnAsync(Guid tenantId, DateOnly day, CancellationToken cancellationToken) =>
        _db.GymMemberships.AsNoTracking().CountAsync(membership => membership.TenantId == tenantId && membership.Status == GymMembershipStatus.Active && membership.EndDate == day, cancellationToken);

    public async Task<IReadOnlyList<DashboardSession>> UpcomingSessionsAsync(Guid tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var rows = await (
            from session in _db.PtSessions.AsNoTracking()
            join member in _db.MemberProfiles.AsNoTracking() on session.MemberProfileId equals member.Id
            where session.TenantId == tenantId && member.TenantId == tenantId && session.Status == PtSessionStatus.Scheduled && session.ScheduledStartUtc >= fromUtc && session.ScheduledStartUtc < toUtc
            orderby session.ScheduledStartUtc
            select new { session.Id, member.FirstName, member.LastName, session.ScheduledStartUtc }).Take(5).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(row => new DashboardSession(row.Id, $"{row.FirstName} {row.LastName}".Trim(), row.ScheduledStartUtc)).ToArray();
    }

    public async Task<IReadOnlyList<DashboardActivity>> ActivitiesAsync(Guid tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var followUps = await (
            from item in _db.LeadFollowUps.AsNoTracking()
            join lead in _db.Leads.AsNoTracking() on item.LeadId equals lead.Id
            where item.TenantId == tenantId && lead.TenantId == tenantId && item.CompletedAt == null && item.ScheduledAt >= fromUtc && item.ScheduledAt < toUtc
            select new { lead.FullName, item.ScheduledAt }).ToListAsync(cancellationToken).ConfigureAwait(false);
        var trials = await (
            from item in _db.LeadTrials.AsNoTracking()
            join lead in _db.Leads.AsNoTracking() on item.LeadId equals lead.Id
            where item.TenantId == tenantId && lead.TenantId == tenantId && item.Status == TrialStatus.Scheduled && item.ScheduledAt >= fromUtc && item.ScheduledAt < toUtc
            select new { lead.FullName, item.ScheduledAt }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return followUps.Select(item => new DashboardActivity("follow-up", item.FullName, item.ScheduledAt))
            .Concat(trials.Select(item => new DashboardActivity("trial", item.FullName, item.ScheduledAt)))
            .OrderBy(item => item.AtUtc)
            .Take(5)
            .ToArray();
    }
}
