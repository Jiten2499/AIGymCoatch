using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class AttendanceRepository : IAttendanceRepository
{
    private readonly ApplicationDbContext _db;

    public AttendanceRepository(ApplicationDbContext db) => _db = db;

    public Task<AttendanceRecord?> GetOpenAsync(Guid tenantId, Guid memberProfileId, CancellationToken cancellationToken) =>
        _db.Attendance.SingleOrDefaultAsync(
            visit => visit.TenantId == tenantId && visit.MemberProfileId == memberProfileId && visit.CheckOutTime == null,
            cancellationToken);

    public Task<AttendanceRecord?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.Attendance.SingleOrDefaultAsync(visit => visit.TenantId == tenantId && visit.Id == id, cancellationToken);

    public async Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken) =>
        await _db.Attendance.AddAsync(record, cancellationToken).ConfigureAwait(false);

    public async Task<PageResult<AttendanceResponse>> ListAsync(
        Guid tenantId,
        Guid? branchId,
        Guid? memberProfileId,
        DateTime? from,
        DateTime? to,
        PageQuery page,
        CancellationToken cancellationToken)
    {
        var query = _db.Attendance.AsNoTracking().Where(visit => visit.TenantId == tenantId);
        if (branchId is not null)
        {
            query = query.Where(visit => visit.BranchId == branchId);
        }

        if (memberProfileId is not null)
        {
            query = query.Where(visit => visit.MemberProfileId == memberProfileId);
        }

        if (from is not null)
        {
            query = query.Where(visit => visit.CheckInTime >= from);
        }

        if (to is not null)
        {
            query = query.Where(visit => visit.CheckInTime < to);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(visit => visit.CheckInTime).Skip(page.Skip).Take(page.NormalizedSize)
            .Select(visit => new AttendanceResponse(visit.Id, visit.BranchId, visit.MemberProfileId, visit.GymMembershipId, visit.CheckInTime, visit.CheckOutTime, visit.CheckInMethod))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<AttendanceResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task AddTokenAsync(QrCheckInToken token, CancellationToken cancellationToken) =>
        await _db.QrCheckInTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public Task<QrCheckInToken?> GetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        _db.QrCheckInTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
}

public sealed class DashboardQuery : IDashboardQuery
{
    private readonly ApplicationDbContext _db;

    public DashboardQuery(ApplicationDbContext db) => _db = db;

    public async Task<BusinessDashboard> GetAsync(
        Guid tenantId,
        Guid? branchId,
        DateTime utcNow,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timeZone);
        var todayStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified), timeZone);
        var monthStartLocal = new DateTime(local.Year, local.Month, 1);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(monthStartLocal, DateTimeKind.Unspecified), timeZone);
        var today = DateOnly.FromDateTime(local);
        var soon = today.AddDays(14);
        var monthDate = DateOnly.FromDateTime(monthStartLocal);

        var members = _db.MemberProfiles.AsNoTracking().Where(profile => profile.TenantId == tenantId && profile.Status == ProfileStatus.Active);
        var memberships = _db.GymMemberships.AsNoTracking().Where(membership => membership.TenantId == tenantId);
        var visits = _db.Attendance.AsNoTracking().Where(visit => visit.TenantId == tenantId);
        if (branchId is not null)
        {
            memberships = memberships.Where(membership => membership.BranchId == branchId);
            visits = visits.Where(visit => visit.BranchId == branchId);
            members = members.Where(profile => memberships.Any(membership => membership.MemberProfileId == profile.Id));
        }

        return new BusinessDashboard(
            await members.CountAsync(cancellationToken).ConfigureAwait(false),
            await members.CountAsync(profile => profile.JoinDate >= monthDate, cancellationToken).ConfigureAwait(false),
            await memberships.CountAsync(membership => membership.Status == GymMembershipStatus.Active && membership.EndDate >= today, cancellationToken).ConfigureAwait(false),
            await memberships.CountAsync(membership => membership.Status == GymMembershipStatus.Expired || (membership.Status == GymMembershipStatus.Active && membership.EndDate < today), cancellationToken).ConfigureAwait(false),
            await memberships.CountAsync(membership => membership.Status == GymMembershipStatus.Active && membership.EndDate >= today && membership.EndDate <= soon, cancellationToken).ConfigureAwait(false),
            await visits.CountAsync(visit => visit.CheckInTime >= todayStart && visit.CheckInTime < todayStart.AddDays(1), cancellationToken).ConfigureAwait(false),
            await visits.CountAsync(visit => visit.CheckInTime >= monthStart, cancellationToken).ConfigureAwait(false),
            await _db.TrainerProfiles.CountAsync(profile => profile.TenantId == tenantId && profile.Status == ProfileStatus.Active, cancellationToken).ConfigureAwait(false),
            await _db.Branches.CountAsync(branch => branch.TenantId == tenantId && branch.Status == BranchStatus.Active && (branchId == null || branch.Id == branchId), cancellationToken).ConfigureAwait(false));
    }

    public async Task<BusinessKpiCounts> CountAsync(
        Guid tenantId,
        DateTime utcNow,
        TimeZoneInfo timeZone,
        KpiScope scope,
        CancellationToken cancellationToken)
    {
        var period = DashboardPeriod.For(utcNow, timeZone);
        var members = await CountMembersAsync(tenantId, period, scope.Members, cancellationToken).ConfigureAwait(false);
        var attendance = scope.Attendance
            ? await _db.Attendance.AsNoTracking().CountAsync(
                visit => visit.TenantId == tenantId && visit.CheckInTime >= period.TodayStartUtc && visit.CheckInTime < period.TomorrowStartUtc,
                cancellationToken).ConfigureAwait(false)
            : 0;
        var memberships = await CountMembershipsAsync(tenantId, period, scope.Memberships, cancellationToken).ConfigureAwait(false);
        var trainers = scope.People
            ? await _db.TrainerProfiles.AsNoTracking().CountAsync(profile => profile.TenantId == tenantId && profile.Status == ProfileStatus.Active, cancellationToken).ConfigureAwait(false)
            : 0;
        var staff = scope.People
            ? await _db.StaffProfiles.AsNoTracking().CountAsync(profile => profile.TenantId == tenantId && profile.Status == ProfileStatus.Active, cancellationToken).ConfigureAwait(false)
            : 0;

        return new BusinessKpiCounts(
            members.Total,
            members.Active,
            members.NewThisMonth,
            attendance,
            memberships.Active,
            memberships.Within7,
            memberships.Within30,
            memberships.Expired,
            trainers,
            staff);
    }

    private async Task<(int Total, int Active, int NewThisMonth)> CountMembersAsync(Guid tenantId, DashboardPeriod period, bool enabled, CancellationToken cancellationToken)
    {
        if (!enabled)
        {
            return (0, 0, 0);
        }

        var profiles = _db.MemberProfiles.AsNoTracking().Where(profile => profile.TenantId == tenantId);
        var row = await profiles.GroupBy(_ => 1).Select(group => new
        {
            Total = group.Count(),
            Active = group.Count(profile => profile.Status == ProfileStatus.Active),
            NewThisMonth = group.Count(profile => profile.Status == ProfileStatus.Active && profile.JoinDate >= period.MonthStart && profile.JoinDate <= period.Today),
        }).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? (0, 0, 0) : (row.Total, row.Active, row.NewThisMonth);
    }

    private async Task<(int Active, int Within7, int Within30, int Expired)> CountMembershipsAsync(Guid tenantId, DashboardPeriod period, bool enabled, CancellationToken cancellationToken)
    {
        if (!enabled)
        {
            return (0, 0, 0, 0);
        }

        var memberships = _db.GymMemberships.AsNoTracking().Where(membership => membership.TenantId == tenantId);
        var row = await memberships.GroupBy(_ => 1).Select(group => new
        {
            Active = group.Count(membership => membership.Status == GymMembershipStatus.Active && membership.EndDate >= period.Today),
            Within7 = group.Count(membership => membership.Status == GymMembershipStatus.Active && membership.EndDate >= period.Today && membership.EndDate <= period.ExpiringThrough7Days),
            Within30 = group.Count(membership => membership.Status == GymMembershipStatus.Active && membership.EndDate >= period.Today && membership.EndDate <= period.ExpiringThrough30Days),
            Expired = group.Count(membership => membership.Status == GymMembershipStatus.Expired || (membership.Status == GymMembershipStatus.Active && membership.EndDate < period.Today)),
        }).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? (0, 0, 0, 0) : (row.Active, row.Within7, row.Within30, row.Expired);
    }
}
