using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class LicenseAdminStore
{
    public async Task<IReadOnlyList<FeatureCatalogResponse>> ListFeaturesAsync(CancellationToken cancellationToken) =>
        await _db.FeatureDefinitions.OrderBy(feature => feature.Category).ThenBy(feature => feature.Code)
            .Select(feature => new FeatureCatalogResponse(feature.Code, feature.Name, feature.Category, feature.Scope.ToString(), feature.IsActive))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<PlanDetailResponse?> GetPlanDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return null;
        }

        var features = await _db.PlanFeatures.Where(feature => feature.PlanId == id).OrderBy(feature => feature.FeatureCode)
            .Select(feature => new PlanFeatureState(feature.FeatureCode, feature.Enabled)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PlanDetailResponse(plan.Id, plan.Code, plan.Name, plan.Description, plan.Status.ToString(), plan.AppliesTo, plan.SortOrder, plan.BaseCapacity, features);
    }

    public async Task<PageResult<TenantLicenseAdminResponse>> ListLicensesAsync(PageQuery page, DateTime utcNow, CancellationToken cancellationToken)
    {
        var query = _db.Subscriptions.Where(subscription => subscription.SubscriberType == SubscriberType.Tenant).OrderBy(subscription => subscription.ExpiresAt);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var subscriptions = await query.Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ids = subscriptions.Select(subscription => subscription.Id).ToArray();
        var tenantIds = subscriptions.Where(subscription => subscription.TenantId != null).Select(subscription => subscription.TenantId!.Value).Distinct().ToArray();
        var tenants = await _db.Tenants.Where(tenant => tenantIds.Contains(tenant.Id)).ToDictionaryAsync(tenant => tenant.Id, tenant => tenant.Name, cancellationToken).ConfigureAwait(false);
        var assigned = await _db.SubscriptionSeats.Where(seat => ids.Contains(seat.SubscriptionId) && seat.Status == SeatStatus.Active)
            .GroupBy(seat => seat.SubscriptionId).Select(group => new { group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken).ConfigureAwait(false);
        var items = new List<TenantLicenseAdminResponse>();
        foreach (var subscription in subscriptions)
        {
            var tenantId = subscription.TenantId ?? Guid.Empty;
            var used = assigned.GetValueOrDefault(subscription.Id);
            var code = await PlanCodeAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false) ?? string.Empty;
            items.Add(new TenantLicenseAdminResponse(subscription.Id, tenantId, tenants.GetValueOrDefault(tenantId) ?? string.Empty, code, subscription.Status.ToString(), subscription.StartsAt, subscription.ExpiresAt, subscription.Seats, 0, used, Math.Max(0, subscription.Seats - used), subscription.IsUsable(utcNow), subscription.RevokedAt));
        }

        return new PageResult<TenantLicenseAdminResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<PageResult<AuditEntryResponse>> ListAuditAsync(PageQuery page, CancellationToken cancellationToken)
    {
        var query = _db.AuditLogs.OrderByDescending(entry => entry.CreatedAt);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.Skip(page.Skip).Take(page.NormalizedSize)
            .Select(entry => new AuditEntryResponse(entry.Id, entry.TenantId, entry.Action, entry.Entity, entry.EntityId, entry.CreatedAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<AuditEntryResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<TenantLicenseAdminResponse?> GetLicenseAdminAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken)
    {
        var subscription = await _db.Subscriptions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return null;
        }

        var tenantId = subscription.TenantId ?? Guid.Empty;
        var used = await _db.SubscriptionSeats.CountAsync(seat => seat.SubscriptionId == subscription.Id && seat.Status == SeatStatus.Active, cancellationToken).ConfigureAwait(false);
        var name = await _db.Tenants.Where(tenant => tenant.Id == tenantId).Select(tenant => tenant.Name).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
        var code = await PlanCodeAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        return new TenantLicenseAdminResponse(subscription.Id, tenantId, name, code, subscription.Status.ToString(), subscription.StartsAt, subscription.ExpiresAt, subscription.Seats, 0, used, Math.Max(0, subscription.Seats - used), subscription.IsUsable(utcNow), subscription.RevokedAt);
    }

    public async Task<IReadOnlyList<WorkspacePlanResponse>> ListWorkspacePlansAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var subscriptions = await _db.Subscriptions
            .Where(subscription => subscription.SubscriberType == SubscriberType.Tenant && subscription.TenantId == tenantId)
            .OrderBy(subscription => subscription.StartsAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var planIds = subscriptions.Select(subscription => subscription.PlanId).Distinct().ToArray();
        var plans = await _db.Plans.Where(plan => planIds.Contains(plan.Id)).ToDictionaryAsync(plan => plan.Id, cancellationToken).ConfigureAwait(false);
        var features = await _db.PlanFeatures.Where(feature => planIds.Contains(feature.PlanId)).OrderBy(feature => feature.FeatureCode).ToListAsync(cancellationToken).ConfigureAwait(false);
        var subscriptionIds = subscriptions.Select(subscription => subscription.Id).ToArray();
        var assigned = await _db.SubscriptionSeats.Where(seat => subscriptionIds.Contains(seat.SubscriptionId) && seat.Status == SeatStatus.Active)
            .GroupBy(seat => seat.SubscriptionId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken)
            .ConfigureAwait(false);
        return subscriptions.Select(subscription =>
        {
            plans.TryGetValue(subscription.PlanId, out var plan);
            var used = assigned.GetValueOrDefault(subscription.Id);
            var planFeatures = features.Where(feature => feature.PlanId == subscription.PlanId).Select(feature => new WorkspacePlanFeature(feature.FeatureCode, feature.Enabled)).ToArray();
            return new WorkspacePlanResponse(subscription.Id, plan?.Code ?? string.Empty, plan?.Name ?? string.Empty, plan?.Description, subscription.Status.ToString(), subscription.StartsAt, subscription.ExpiresAt, subscription.Seats, used, Math.Max(0, subscription.Seats - used), subscription.IsUsable(utcNow), planFeatures);
        }).ToArray();
    }

    public async Task<IReadOnlyList<WorkspacePlanAssignee>> ListActivePlanAssigneesAsync(Guid tenantId, Guid subscriptionId, CancellationToken cancellationToken) =>
        await (
            from seat in _db.SubscriptionSeats
            join subscription in _db.Subscriptions on seat.SubscriptionId equals subscription.Id
            join user in _db.Users on seat.UserId equals user.Id
            where subscription.TenantId == tenantId
                && subscription.Id == subscriptionId
                && subscription.SubscriberType == SubscriberType.Tenant
                && seat.Status == SeatStatus.Active
            orderby user.LastName, user.FirstName, user.Email
            select new WorkspacePlanAssignee(seat.Id, seat.UserId, user.Email, user.FirstName, user.LastName, seat.AssignedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, UserPlanAssignment>> ActiveUserPlansAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await (
            from seat in _db.SubscriptionSeats
            join subscription in _db.Subscriptions on seat.SubscriptionId equals subscription.Id
            join plan in _db.Plans on subscription.PlanId equals plan.Id
            where subscription.TenantId == tenantId && subscription.SubscriberType == SubscriberType.Tenant && seat.Status == SeatStatus.Active
            orderby seat.AssignedAt descending
            select new { seat.UserId, seat.Id, plan.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.GroupBy(row => row.UserId).ToDictionary(group => group.Key, group => new UserPlanAssignment(group.First().Id, group.First().Name));
    }

    public Task<Subscription?> GetTenantSubscriptionAsync(Guid tenantId, Guid subscriptionId, CancellationToken cancellationToken) =>
        _db.Subscriptions.FirstOrDefaultAsync(
            subscription => subscription.Id == subscriptionId && subscription.TenantId == tenantId && subscription.SubscriberType == SubscriberType.Tenant,
            cancellationToken);

    public async Task<IReadOnlyList<SubscriptionSeat>> ActiveSeatsForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        await (from seat in _db.SubscriptionSeats
               join subscription in _db.Subscriptions on seat.SubscriptionId equals subscription.Id
               where subscription.TenantId == tenantId && seat.UserId == userId && seat.Status == SeatStatus.Active
               select seat).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<TenantMemberOption>> ListMembersAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await (from member in _db.TenantMembers
               join user in _db.Users on member.UserId equals user.Id
               where member.TenantId == tenantId && member.Status == MembershipStatus.Active
               orderby user.Email
               select new TenantMemberOption(user.Id, user.Email, user.FirstName, user.LastName)).ToListAsync(cancellationToken).ConfigureAwait(false);
}
