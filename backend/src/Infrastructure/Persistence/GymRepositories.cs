using LifeFitAI.Application.Gym;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class BusinessProfileRepository : IBusinessProfileRepository
{
    private readonly ApplicationDbContext _db;

    public BusinessProfileRepository(ApplicationDbContext db) => _db = db;

    public Task<BusinessProfile?> GetAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.BusinessProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId, cancellationToken);

    public async Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) =>
        await _db.BusinessProfiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);
}

public sealed class BranchRepository : IBranchRepository
{
    private readonly ApplicationDbContext _db;

    public BranchRepository(ApplicationDbContext db) => _db = db;

    public Task<Branch?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.Branches.SingleOrDefaultAsync(branch => branch.TenantId == tenantId && branch.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid tenantId, string code, CancellationToken cancellationToken) =>
        _db.Branches.AnyAsync(branch => branch.TenantId == tenantId && branch.Code == code, cancellationToken);

    public async Task AddAsync(Branch branch, CancellationToken cancellationToken) =>
        await _db.Branches.AddAsync(branch, cancellationToken).ConfigureAwait(false);

    public async Task<PageResult<BranchResponse>> ListAsync(Guid tenantId, PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.Branches.AsNoTracking().Where(branch => branch.TenantId == tenantId);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(branch => branch.Name).Skip(page.Skip).Take(page.NormalizedSize)
            .Select(branch => new BranchResponse(
                branch.Id, branch.Name, branch.Code, branch.Address, branch.City, branch.State, branch.Country,
                branch.PostalCode, branch.Phone, branch.Email, branch.TimeZone, branch.Status))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<BranchResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }
}

public sealed class MembershipRepository : IMembershipRepository
{
    private readonly ApplicationDbContext _db;

    public MembershipRepository(ApplicationDbContext db) => _db = db;

    public Task<MembershipPlan?> GetPlanAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.MembershipPlans.SingleOrDefaultAsync(plan => plan.TenantId == tenantId && plan.Id == id, cancellationToken);

    public Task<bool> PlanNameExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken) =>
        _db.MembershipPlans.AnyAsync(plan => plan.TenantId == tenantId && plan.Name == name, cancellationToken);

    public async Task AddPlanAsync(MembershipPlan plan, CancellationToken cancellationToken) =>
        await _db.MembershipPlans.AddAsync(plan, cancellationToken).ConfigureAwait(false);

    public async Task<PageResult<PlanResponse>> ListPlansAsync(Guid tenantId, bool? activeOnly, string? term, PackageCategory? category, PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.MembershipPlans.AsNoTracking().Where(plan => plan.TenantId == tenantId);
        if (activeOnly == true)
        {
            query = query.Where(plan => plan.IsActive);
        }

        if (category is not null)
        {
            query = query.Where(plan => plan.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var value = term.Trim();
            query = query.Where(plan => plan.Name.Contains(value) || (plan.Description != null && plan.Description.Contains(value)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(plan => plan.Name).Skip(page.Skip).Take(page.NormalizedSize)
            .Select(plan => new PlanResponse(plan.Id, plan.Name, plan.Description, plan.DurationDays, plan.MembershipType, plan.MaxVisits, plan.Price, plan.Currency, plan.IsActive, plan.Category, plan.DurationUnit, plan.DurationCount, plan.IncludedPtSessions))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<PlanResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task<GymMembership?> GetMembershipAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.GymMemberships.SingleOrDefaultAsync(membership => membership.TenantId == tenantId && membership.Id == id, cancellationToken);

    public Task<bool> OverlapsMembershipAsync(Guid tenantId, Guid memberProfileId, DateOnly start, DateOnly end, Guid? excludeMembershipId, CancellationToken cancellationToken) =>
        _db.GymMemberships.AnyAsync(membership =>
            membership.TenantId == tenantId
            && membership.MemberProfileId == memberProfileId
            && membership.Id != excludeMembershipId
            && (membership.Status == GymMembershipStatus.Frozen
                || ((membership.Status == GymMembershipStatus.Active || membership.Status == GymMembershipStatus.Pending)
                    && membership.StartDate <= end
                    && start <= membership.EndDate)), cancellationToken);

    public async Task AddMembershipAsync(GymMembership membership, CancellationToken cancellationToken) =>
        await _db.GymMemberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);

    public Task<MembershipFreeze?> GetOpenFreezeAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken) =>
        _db.MembershipFreezes.SingleOrDefaultAsync(freeze => freeze.TenantId == tenantId && freeze.GymMembershipId == membershipId && freeze.EndDate == null, cancellationToken);

    public Task<bool> FreezeOverlapsAsync(Guid tenantId, Guid membershipId, DateOnly start, CancellationToken cancellationToken) =>
        _db.MembershipFreezes.AnyAsync(freeze => freeze.TenantId == tenantId && freeze.GymMembershipId == membershipId && freeze.StartDate <= start && (freeze.EndDate == null || start <= freeze.EndDate), cancellationToken);

    public async Task AddFreezeAsync(MembershipFreeze freeze, CancellationToken cancellationToken) =>
        await _db.MembershipFreezes.AddAsync(freeze, cancellationToken).ConfigureAwait(false);

    public Task<int> CountFreezesAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken) =>
        _db.MembershipFreezes.CountAsync(freeze => freeze.TenantId == tenantId && freeze.GymMembershipId == membershipId, cancellationToken);

    public async Task<PageResult<GymMembershipResponse>> SearchMembershipsAsync(
        Guid tenantId,
        GymMembershipStatus? status,
        Guid? planId,
        Guid? branchId,
        Guid? memberProfileId,
        string? term,
        DateOnly? startsFrom,
        DateOnly? startsTo,
        DateOnly? expiresFrom,
        DateOnly? expiresTo,
        DateOnly today,
        string? sort,
        PageQuery page,
        CancellationToken cancellationToken)
    {
        var query =
            from membership in _db.GymMemberships.IgnoreQueryFilters().AsNoTracking()
            join profile in _db.MemberProfiles.AsNoTracking() on membership.MemberProfileId equals profile.Id
            join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            join plan in _db.MembershipPlans.AsNoTracking() on membership.MembershipPlanId equals plan.Id
            where membership.TenantId == tenantId && membership.DeletedAt == null
            select new { membership, profile, user, plan };
        if (status == GymMembershipStatus.Expired)
        {
            query = query.Where(row => row.membership.Status == GymMembershipStatus.Expired || ((row.membership.Status == GymMembershipStatus.Active || row.membership.Status == GymMembershipStatus.Pending) && row.membership.EndDate < today));
        }
        else if (status == GymMembershipStatus.Active)
        {
            query = query.Where(row => row.membership.Status == GymMembershipStatus.Active && row.membership.StartDate <= today && row.membership.EndDate >= today);
        }
        else if (status == GymMembershipStatus.Pending)
        {
            query = query.Where(row => row.membership.Status == GymMembershipStatus.Pending && row.membership.StartDate > today);
        }
        else if (status is not null)
        {
            query = query.Where(row => row.membership.Status == status);
        }

        if (planId is not null)
        {
            query = query.Where(row => row.membership.MembershipPlanId == planId);
        }

        if (branchId is not null)
        {
            query = query.Where(row => row.membership.BranchId == branchId);
        }

        if (memberProfileId is not null)
        {
            query = query.Where(row => row.membership.MemberProfileId == memberProfileId);
        }

        if (startsFrom is not null)
        {
            query = query.Where(row => row.membership.StartDate >= startsFrom);
        }

        if (startsTo is not null)
        {
            query = query.Where(row => row.membership.StartDate <= startsTo);
        }

        if (expiresFrom is not null)
        {
            query = query.Where(row => row.membership.EndDate >= expiresFrom);
        }

        if (expiresTo is not null)
        {
            query = query.Where(row => row.membership.EndDate <= expiresTo);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var value = term.Trim();
            query = query.Where(row => row.profile.FirstName.Contains(value) || row.profile.LastName.Contains(value) || (row.user != null && (row.user.FirstName.Contains(value) || row.user.LastName.Contains(value))) || row.plan.Name.Contains(value) || row.membership.MembershipNumber.Contains(value));
        }

        query = sort switch
        {
            "startDate" => query.OrderBy(row => row.membership.StartDate),
            "name" => query.OrderBy(row => row.profile.LastName).ThenBy(row => row.profile.FirstName),
            _ => query.OrderByDescending(row => row.membership.EndDate),
        };
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        var membershipIds = rows.Select(row => row.membership.Id).ToList();
        var paidRows = await _db.MembershipPayments.AsNoTracking()
            .Where(payment => payment.TenantId == tenantId
                && membershipIds.Contains(payment.GymMembershipId)
                && payment.Status == MembershipPaymentStatus.Posted)
            .GroupBy(payment => payment.GymMembershipId)
            .Select(group => new { MembershipId = group.Key, Total = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var paidMap = paidRows.ToDictionary(row => row.MembershipId, row => row.Total);
        var items = rows.Select(row =>
        {
            var paid = paidMap.GetValueOrDefault(row.membership.Id);
            return new GymMembershipResponse(
                row.membership.Id,
                row.membership.MemberProfileId,
                row.membership.BranchId,
                row.membership.MembershipPlanId,
                row.membership.MembershipNumber,
                row.membership.StartDate,
                row.membership.EndDate,
                MembershipCalendar.EffectiveStatus(row.membership.Status, row.membership.StartDate, row.membership.EndDate, today),
                row.membership.AutoRenew,
                row.membership.Notes,
                row.profile.FirstName != "" || row.profile.LastName != ""
                    ? (row.profile.FirstName + " " + row.profile.LastName).Trim()
                    : row.user == null ? string.Empty : row.user.FirstName + " " + row.user.LastName,
                row.plan.Name,
                row.membership.PackagePrice,
                row.membership.DiscountAmount,
                row.membership.FinalAmount,
                row.membership.Currency,
                row.membership.DiscountRuleId,
                row.membership.DiscountName,
                paid,
                MembershipPaymentRules.Outstanding(row.membership.FinalAmount, paid),
                MembershipPaymentRules.Settlement(row.membership.FinalAmount, paid),
                null,
                row.membership.IncludedPtSessions,
                row.membership.PtSessionsRemaining);
        }).ToList();
        return new PageResult<GymMembershipResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task AddHistoryAsync(MembershipHistoryEntry entry, CancellationToken cancellationToken) =>
        await _db.MembershipHistory.AddAsync(entry, cancellationToken).ConfigureAwait(false);

    public async Task<PageResult<GymMembershipResponse>> ListMembershipsAsync(
        Guid tenantId,
        GymMembershipStatus? status,
        Guid? planId,
        Guid? branchId,
        DateOnly? expiringFrom,
        DateOnly? expiringTo,
        PageQuery page,
        CancellationToken cancellationToken)
    {
        var query = _db.GymMemberships.AsNoTracking().Where(membership => membership.TenantId == tenantId);
        if (status is not null)
        {
            query = query.Where(membership => membership.Status == status);
        }

        if (planId is not null)
        {
            query = query.Where(membership => membership.MembershipPlanId == planId);
        }

        if (branchId is not null)
        {
            query = query.Where(membership => membership.BranchId == null || membership.BranchId == branchId);
        }

        if (expiringFrom is not null)
        {
            query = query.Where(membership => membership.EndDate >= expiringFrom);
        }

        if (expiringTo is not null)
        {
            query = query.Where(membership => membership.EndDate <= expiringTo);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.OrderByDescending(membership => membership.EndDate).Skip(page.Skip).Take(page.NormalizedSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var membershipIds = rows.Select(membership => membership.Id).ToList();
        var paidRows = await _db.MembershipPayments.AsNoTracking()
            .Where(payment => payment.TenantId == tenantId
                && membershipIds.Contains(payment.GymMembershipId)
                && payment.Status == MembershipPaymentStatus.Posted)
            .GroupBy(payment => payment.GymMembershipId)
            .Select(group => new { MembershipId = group.Key, Total = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var paidMap = paidRows.ToDictionary(row => row.MembershipId, row => row.Total);
        var items = rows.Select(membership =>
        {
            var paid = paidMap.GetValueOrDefault(membership.Id);
            return new GymMembershipResponse(
                membership.Id, membership.MemberProfileId, membership.BranchId, membership.MembershipPlanId,
                membership.MembershipNumber, membership.StartDate, membership.EndDate, membership.Status, membership.AutoRenew, membership.Notes, null, null,
                membership.PackagePrice, membership.DiscountAmount, membership.FinalAmount, membership.Currency,
                membership.DiscountRuleId, membership.DiscountName, paid,
                MembershipPaymentRules.Outstanding(membership.FinalAmount, paid),
                MembershipPaymentRules.Settlement(membership.FinalAmount, paid),
                null,
                membership.IncludedPtSessions,
                membership.PtSessionsRemaining);
        }).ToList();
        return new PageResult<GymMembershipResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<IReadOnlyList<MembershipHistoryResponse>> ListHistoryAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken)
    {
        return await _db.MembershipHistory.AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && entry.GymMembershipId == membershipId)
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(100)
            .Select(entry => new MembershipHistoryResponse(entry.Id, entry.Action, entry.PreviousStatus, entry.NewStatus, entry.EffectiveDate, entry.Reason, entry.PerformedByUserId, entry.CreatedAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CountVisitsAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken) =>
        _db.Attendance.CountAsync(visit => visit.TenantId == tenantId && visit.GymMembershipId == membershipId, cancellationToken);
}
