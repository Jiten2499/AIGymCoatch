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

    public async Task<PageResult<PlanResponse>> ListPlansAsync(Guid tenantId, bool? activeOnly, PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.MembershipPlans.AsNoTracking().Where(plan => plan.TenantId == tenantId);
        if (activeOnly == true)
        {
            query = query.Where(plan => plan.IsActive);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(plan => plan.Name).Skip(page.Skip).Take(page.NormalizedSize)
            .Select(plan => new PlanResponse(plan.Id, plan.Name, plan.Description, plan.DurationDays, plan.MembershipType, plan.MaxVisits, plan.Price, plan.Currency, plan.IsActive))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<PlanResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task<GymMembership?> GetMembershipAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.GymMemberships.SingleOrDefaultAsync(membership => membership.TenantId == tenantId && membership.Id == id, cancellationToken);

    public async Task AddMembershipAsync(GymMembership membership, CancellationToken cancellationToken) =>
        await _db.GymMemberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);

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
            query = query.Where(membership => membership.BranchId == branchId);
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
        var items = await query.OrderByDescending(membership => membership.EndDate).Skip(page.Skip).Take(page.NormalizedSize)
            .Select(membership => new GymMembershipResponse(
                membership.Id, membership.MemberProfileId, membership.BranchId, membership.MembershipPlanId,
                membership.MembershipNumber, membership.StartDate, membership.EndDate, membership.Status, membership.AutoRenew, membership.Notes))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
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
