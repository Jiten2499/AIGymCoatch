using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class LicenseAdminStore : ILicenseAdminStore, IPlanDefinitionRepository
{
    private readonly ApplicationDbContext _db;

    public LicenseAdminStore(ApplicationDbContext db) => _db = db;

    public Task<PlanDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        _db.Plans.FirstOrDefaultAsync(plan => plan.Code == code, cancellationToken);

    public async Task<IReadOnlyList<PlanFeature>> GetFeaturesAsync(Guid planId, CancellationToken cancellationToken) =>
        await _db.PlanFeatures.Where(feature => feature.PlanId == planId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> PlanCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        _db.Plans.AnyAsync(plan => plan.Code == code, cancellationToken);

    public async Task AddPlanAsync(PlanDefinition plan, CancellationToken cancellationToken) =>
        await _db.Plans.AddAsync(plan, cancellationToken).ConfigureAwait(false);

    public Task<PlanDefinition?> GetPlanAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Plans.FirstOrDefaultAsync(plan => plan.Id == id, cancellationToken);

    public async Task SetFeatureAsync(Guid planId, string featureCode, bool enabled, DateTime utcNow, CancellationToken cancellationToken)
    {
        var row = await _db.PlanFeatures.FirstOrDefaultAsync(feature => feature.PlanId == planId && feature.FeatureCode == featureCode, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            await _db.PlanFeatures.AddAsync(PlanFeature.Create(Guid.NewGuid(), planId, featureCode, enabled, utcNow), cancellationToken).ConfigureAwait(false);
            return;
        }

        row.SetEnabled(enabled, utcNow);
    }

    public async Task<IReadOnlyList<Guid>> TenantsForPlanAsync(Guid planId, CancellationToken cancellationToken) =>
        await _db.Subscriptions
            .Where(subscription => subscription.PlanId == planId && subscription.TenantId != null)
            .Select(subscription => subscription.TenantId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PlanResponse>> ListPlansAsync(CancellationToken cancellationToken) =>
        await _db.Plans.OrderBy(plan => plan.SortOrder)
            .Select(plan => new PlanResponse(plan.Id, plan.Code, plan.Name, plan.Status.ToString(), plan.AppliesTo, plan.BaseCapacity))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Subscription?> GetSubscriptionAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Subscriptions.FirstOrDefaultAsync(subscription => subscription.Id == id, cancellationToken);

    public Task<Subscription?> GetActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.Subscriptions.FirstOrDefaultAsync(
            subscription => subscription.SubscriberType == SubscriberType.Tenant
                && subscription.TenantId == tenantId
                && subscription.Status == SubscriptionStatus.Active,
            cancellationToken);

    public async Task<Guid?> SubscriptionTenantAsync(Guid subscriptionId, CancellationToken cancellationToken) =>
        await _db.Subscriptions.Where(subscription => subscription.Id == subscriptionId)
            .Select(subscription => subscription.TenantId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    public Task<int> ActiveSeatCountAsync(Guid subscriptionId, CancellationToken cancellationToken) =>
        _db.SubscriptionSeats.CountAsync(seat => seat.SubscriptionId == subscriptionId && seat.Status == SeatStatus.Active, cancellationToken);

    public Task<SubscriptionSeat?> ActiveSeatForUserAsync(Guid subscriptionId, Guid userId, CancellationToken cancellationToken) =>
        _db.SubscriptionSeats.FirstOrDefaultAsync(seat => seat.SubscriptionId == subscriptionId && seat.UserId == userId && seat.Status == SeatStatus.Active, cancellationToken);

    public async Task AddSeatAsync(SubscriptionSeat seat, CancellationToken cancellationToken) =>
        await _db.SubscriptionSeats.AddAsync(seat, cancellationToken).ConfigureAwait(false);

    public Task<SubscriptionSeat?> GetSeatAsync(Guid id, CancellationToken cancellationToken) =>
        _db.SubscriptionSeats.FirstOrDefaultAsync(seat => seat.Id == id, cancellationToken);

    public async Task<IReadOnlyList<LicenseAssignmentResponse>> ListAssignmentsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await (from seat in _db.SubscriptionSeats
               join subscription in _db.Subscriptions on seat.SubscriptionId equals subscription.Id
               where subscription.TenantId == tenantId
               orderby seat.AssignedAt descending
               select new LicenseAssignmentResponse(seat.Id, seat.UserId, seat.Status.ToString(), seat.AssignedAt, seat.ReleasedAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ExpiringLicenseResponse>> ExpiringAsync(DateTime until, CancellationToken cancellationToken)
    {
        var rows = await _db.Subscriptions
            .Where(subscription => subscription.SubscriberType == SubscriberType.Tenant
                && subscription.Status == SubscriptionStatus.Active
                && subscription.ExpiresAt != null
                && subscription.ExpiresAt <= until)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<ExpiringLicenseResponse>();
        foreach (var subscription in rows)
        {
            result.Add(new ExpiringLicenseResponse(
                subscription.Id,
                subscription.TenantId ?? Guid.Empty,
                await PlanCodeAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false) ?? string.Empty,
                subscription.ExpiresAt));
        }

        return result;
    }

    public async Task<string?> PlanCodeAsync(Guid planId, CancellationToken cancellationToken) =>
        (await _db.Plans.FirstOrDefaultAsync(plan => plan.Id == planId, cancellationToken).ConfigureAwait(false))?.Code;

    public Task LockSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken) =>
        _db.Database.ExecuteSqlInterpolatedAsync(
            $@"DECLARE @LockedId uniqueidentifier;
SELECT @LockedId = Id FROM app.Subscriptions WITH (UPDLOCK, HOLDLOCK) WHERE Id = {subscriptionId} AND DeletedAt IS NULL;",
            cancellationToken);

    public async Task<SeatReservation> ReserveSeatAsync(Guid tenantId, Guid subscriptionId, Guid userId, Guid? assignedBy, DateTime utcNow, CancellationToken cancellationToken)
    {
        await LockSubscriptionAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        var subscription = await _db.Subscriptions.FirstOrDefaultAsync(item => item.Id == subscriptionId && item.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return SeatReservation.Fail(new Error("license.not_found", "This workspace does not have a usable subscription."));
        }

        await _db.Entry(subscription).ReloadAsync(cancellationToken).ConfigureAwait(false);
        if (!subscription.IsUsable(utcNow))
        {
            return SeatReservation.Fail(new Error("license.not_found", "This workspace does not have a usable subscription."));
        }

        if (await ActiveSeatForUserAsync(subscriptionId, userId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return SeatReservation.Fail(new Error("license.assignment_conflict", "That person already has an active seat."));
        }

        var assigned = await ActiveSeatCountAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        if (!SeatAllocator.HasCapacity(subscription.Seats, assigned))
        {
            return SeatReservation.Fail(new Error("license.capacity_conflict", "No seats are available."));
        }

        var seat = SubscriptionSeat.Create(Guid.NewGuid(), subscriptionId, userId, assignedBy, utcNow);
        await AddSeatAsync(seat, cancellationToken).ConfigureAwait(false);
        return SeatReservation.Ok(seat);
    }

    public async Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken) =>
        await _db.Subscriptions.AddAsync(subscription, cancellationToken).ConfigureAwait(false);

    public Task<Subscription?> GetActiveUserSubscriptionAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Subscriptions.FirstOrDefaultAsync(
            subscription => subscription.SubscriberType == SubscriberType.User
                && subscription.UserId == userId
                && subscription.Status == SubscriptionStatus.Active,
            cancellationToken);
}

public sealed class EfEntitlementStore : IEntitlementStore
{
    private readonly ApplicationDbContext _db;

    public EfEntitlementStore(ApplicationDbContext db) => _db = db;

    public async Task<bool?> PlanFeatureAsync(Guid planId, string featureCode, CancellationToken cancellationToken)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(item => item.Id == planId, cancellationToken).ConfigureAwait(false);
        if (plan is null || plan.Status != PlanStatus.Active)
        {
            return false;
        }

        var feature = await _db.PlanFeatures.FirstOrDefaultAsync(item => item.PlanId == planId && item.FeatureCode == featureCode, cancellationToken).ConfigureAwait(false);
        return feature?.Enabled ?? false;
    }

    public async Task<bool> RequiresSeatAsync(string featureCode, CancellationToken cancellationToken)
    {
        var feature = await _db.FeatureDefinitions.FirstOrDefaultAsync(item => item.Code == featureCode && item.IsActive, cancellationToken).ConfigureAwait(false);
        return feature is { Scope: FeatureScope.User };
    }

    public Task<bool> HasSeatAsync(Guid subscriptionId, Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        _db.SubscriptionSeats.AnyAsync(seat => seat.SubscriptionId == subscriptionId && seat.UserId == userId && seat.Status == SeatStatus.Active, cancellationToken);
}
