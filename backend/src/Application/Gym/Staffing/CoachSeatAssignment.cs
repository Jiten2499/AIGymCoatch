using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Staffing;

internal static class CoachSeatAssignment
{
    public static async Task ValidateCapacityAsync(
        ILicenseAdminStore licenses,
        Guid tenantId,
        Guid subscriptionId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var subscription = await licenses.GetTenantSubscriptionAsync(tenantId, subscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsUsable(utcNow))
        {
            throw new MemberOnboardingException(new Error("license.not_found", "That plan is not assigned to this workspace."));
        }

        var used = await licenses.ActiveSeatCountAsync(subscription.Id, cancellationToken).ConfigureAwait(false);
        if (!SeatAllocator.HasCapacity(subscription.Seats, used))
        {
            throw new MemberOnboardingException(new Error("license.capacity_conflict", "No seats are available on that plan."));
        }
    }

    public static async Task AssignAsync(
        ILicenseAdminStore licenses,
        IPermissionCacheInvalidator cache,
        Guid tenantId,
        Guid subscriptionId,
        Guid userId,
        Guid actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var current = await licenses.ActiveSeatsForUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (current.Any(seat => seat.IsActive && seat.SubscriptionId == subscriptionId))
        {
            return;
        }

        var subscription = await licenses.GetTenantSubscriptionAsync(tenantId, subscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsUsable(utcNow))
        {
            throw new MemberOnboardingException(new Error("license.not_found", "That plan is not assigned to this workspace."));
        }

        foreach (var seat in current.Where(seat => seat.IsActive))
        {
            seat.Release(utcNow, actorId);
        }

        var reservation = await licenses.ReserveSeatAsync(tenantId, subscriptionId, userId, actorId, utcNow, cancellationToken).ConfigureAwait(false);
        if (!reservation.Succeeded)
        {
            throw new MemberOnboardingException(reservation.Error ?? new Error("license.capacity_conflict", "No seats are available on that plan."));
        }

        cache.Schedule(tenantId, userId);
        cache.ScheduleFeatures(tenantId);
    }
}
