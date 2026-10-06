using FluentAssertions;
using LifeFitAI.Application.Tests.Identity;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Licensing;

public sealed class LicensingEngineTests
{
    [Fact]
    public void Expired_or_cancelled_subscription_is_not_usable()
    {
        var now = DateTime.UtcNow;
        var subscription = Subscription.ForTenant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, 0);
        subscription.SetExpiresAt(now.AddMinutes(-1));
        subscription.IsUsable(now).Should().BeFalse();

        var cancelled = Subscription.ForTenant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now, 5);
        cancelled.Cancel(now, Guid.NewGuid());
        cancelled.IsUsable(now).Should().BeFalse();
        cancelled.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void Available_seats_are_total_minus_assigned()
    {
        var total = 125;
        var available = total - 72;
        SeatAllocator.HasCapacity(total, 72).Should().BeTrue();
        available.Should().Be(53);
        SeatAllocator.HasCapacity(total, total).Should().BeFalse();
    }

    [Fact]
    public void Retired_plan_cannot_be_activated()
    {
        var plan = PlanDefinition.Create(Guid.NewGuid(), "GYM_STARTER", "Gym Starter", null, "Business", 6, 25);
        plan.Activate();
        plan.Deactivate().Should().BeTrue();
        plan.Retire();
        plan.Activate().Should().BeFalse();
        plan.Status.Should().Be(PlanStatus.Retired);
    }

    [Fact]
    public async Task Member_cannot_assign_a_seat()
    {
        var handler = Handler(manage: false, superAdmin: false);
        var result = await handler.Handle(new AssignUserLicenseCommand(Guid.NewGuid()), CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(IdentityErrors.Forbidden.Code);
    }

    [Fact]
    public async Task Full_pool_rejects_another_assignment()
    {
        var handler = Handler(manage: true, superAdmin: false, capacity: 1, assigned: 1);
        var result = await handler.Handle(new AssignUserLicenseCommand(Guid.NewGuid()), CancellationToken.None);
        result.Error.Code.Should().Be("license.capacity_conflict");
    }

    [Fact]
    public async Task Personal_owner_cannot_list_platform_plans()
    {
        var handler = Handler(manage: false, superAdmin: false);
        var result = await handler.Handle(new ListPlansQuery(), CancellationToken.None);
        result.Error.Code.Should().Be(IdentityErrors.Forbidden.Code);
    }

    private static LicenseAdminHandler Handler(bool manage, bool superAdmin, int capacity = 0, int assigned = 0)
    {
        var actor = new FakeActor { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid() };
        var roles = Substitute.For<IRoleRepository>();
        roles.GetRoleNamesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(superAdmin ? new[] { RoleNames.SuperAdmin } : new[] { RoleNames.PersonalOwner });
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), PermissionCodes.LicenseManage, Arg.Any<CancellationToken>()).Returns(manage);
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), PermissionCodes.PlatformAdmin, Arg.Any<CancellationToken>()).Returns(superAdmin);
        var members = Substitute.For<ITenantMembershipRepository>();
        members.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(TenantMember.Create(Guid.NewGuid(), actor.TenantId!.Value, Guid.NewGuid(), DateTime.UtcNow));
        var subscription = Subscription.ForTenant(Guid.NewGuid(), actor.TenantId!.Value, Guid.NewGuid(), DateTime.UtcNow, capacity);
        var store = Substitute.For<ILicenseAdminStore>();
        store.GetActiveSubscriptionAsync(actor.TenantId.Value, Arg.Any<CancellationToken>()).Returns(subscription);
        store.ReserveSeatAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(assigned >= capacity
                ? SeatReservation.Fail(new Error("license.capacity_conflict", "No seats are available."))
                : SeatReservation.Ok(SubscriptionSeat.Create(Guid.NewGuid(), subscription.Id, Guid.NewGuid(), actor.UserId, DateTime.UtcNow)));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        return new LicenseAdminHandler(actor, roles, permissions, members, store, Substitute.For<IAuditLogRepository>(), Substitute.For<IPermissionCacheInvalidator>(), unitOfWork, clock, Substitute.For<IClientContext>());
    }
}
