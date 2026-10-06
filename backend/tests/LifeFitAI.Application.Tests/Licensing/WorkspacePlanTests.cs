using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Application.Tests.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Licensing;

public sealed class WorkspacePlanTests
{
    [Fact]
    public async Task Owner_SeesOnlyPlansAssignedToTheCurrentWorkspace()
    {
        var actor = Actor();
        var store = Substitute.For<ILicenseAdminStore>();
        var plan = new WorkspacePlanResponse(Guid.NewGuid(), "PRO", "Pro", null, "Active", DateTime.UtcNow, null, 10, 1, 9, true, [new WorkspacePlanFeature("AI_COACH", true)]);
        store.ListWorkspacePlansAsync(actor.TenantId!.Value, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new[] { plan });
        var handler = new ListWorkspacePlansQueryHandler(actor, Permissions(licenseRead: true), Tenants(actor), store, Clock());

        var result = await handler.Handle(new ListWorkspacePlansQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(item => item.PlanCode == "PRO" && item.Seats == 10 && item.Available == 9);
        await store.Received(1).ListWorkspacePlansAsync(actor.TenantId.Value, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_AssignsAWorkspacePlanAndCannotUseAnotherWorkspacePlan()
    {
        var actor = Actor();
        var userId = Guid.NewGuid();
        var subscription = Subscription.ForTenant(Guid.NewGuid(), actor.TenantId!.Value, Guid.NewGuid(), DateTime.UtcNow, 5);
        var store = Substitute.For<ILicenseAdminStore>();
        store.GetTenantSubscriptionAsync(actor.TenantId.Value, subscription.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        store.GetTenantSubscriptionAsync(actor.TenantId.Value, Arg.Is<Guid>(id => id != subscription.Id), Arg.Any<CancellationToken>()).Returns((Subscription?)null);
        store.ActiveSeatsForUserAsync(actor.TenantId.Value, userId, Arg.Any<CancellationToken>()).Returns(Array.Empty<SubscriptionSeat>());
        store.ReserveSeatAsync(actor.TenantId.Value, subscription.Id, userId, actor.UserId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(SeatReservation.Ok(SubscriptionSeat.Create(Guid.NewGuid(), subscription.Id, userId, actor.UserId, DateTime.UtcNow)));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        var handler = new AssignWorkspacePlanCommandHandler(actor, Permissions(licenseManage: true), Tenants(actor), Members(), store, Substitute.For<IAuditLogRepository>(), Substitute.For<IPermissionCacheInvalidator>(), unitOfWork, Clock(), Substitute.For<IClientContext>());

        var assigned = await handler.Handle(new AssignWorkspacePlanCommand(userId, subscription.Id), CancellationToken.None);
        var foreign = await handler.Handle(new AssignWorkspacePlanCommand(userId, Guid.NewGuid()), CancellationToken.None);

        assigned.IsSuccess.Should().BeTrue();
        foreign.Error.Code.Should().Be("license.not_found");
    }

    private static FakeActor Actor() => new() { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid() };

    private static IPermissionService Permissions(bool licenseRead = false, bool licenseManage = false)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), PermissionCodes.LicenseRead, Arg.Any<CancellationToken>()).Returns(licenseRead);
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), PermissionCodes.LicenseManage, Arg.Any<CancellationToken>()).Returns(licenseManage);
        return permissions;
    }

    private static ITenantRepository Tenants(FakeActor actor)
    {
        var tenant = Tenant.Create(actor.TenantId!.Value, "Gym", TenantType.Gym);
        var tenants = Substitute.For<ITenantRepository>();
        tenants.GetAsync(actor.TenantId.Value, Arg.Any<CancellationToken>()).Returns(tenant);
        return tenants;
    }

    private static ITenantMembershipRepository Members()
    {
        var members = Substitute.For<ITenantMembershipRepository>();
        members.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(TenantMember.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));
        return members;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        return clock;
    }
}
