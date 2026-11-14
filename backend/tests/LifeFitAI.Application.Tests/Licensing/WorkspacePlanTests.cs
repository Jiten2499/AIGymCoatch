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

    [Fact]
    public async Task Owner_ListsAssigneesForWorkspacePlanAndRejectsForeignPlan()
    {
        var actor = Actor();
        var subscription = Subscription.ForTenant(Guid.NewGuid(), actor.TenantId!.Value, Guid.NewGuid(), DateTime.UtcNow, 5);
        var assignee = new WorkspacePlanAssignee(Guid.NewGuid(), Guid.NewGuid(), "coach@example.com", "Ada", "Lovelace", DateTime.UtcNow);
        var store = Substitute.For<ILicenseAdminStore>();
        store.GetTenantSubscriptionAsync(actor.TenantId.Value, subscription.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        store.GetTenantSubscriptionAsync(actor.TenantId.Value, Arg.Is<Guid>(id => id != subscription.Id), Arg.Any<CancellationToken>()).Returns((Subscription?)null);
        store.ListActivePlanAssigneesAsync(actor.TenantId.Value, subscription.Id, Arg.Any<CancellationToken>()).Returns(new[] { assignee });
        var handler = new ListWorkspacePlanAssigneesQueryHandler(actor, Permissions(licenseRead: true), Tenants(actor), store);

        var listed = await handler.Handle(new ListWorkspacePlanAssigneesQuery(subscription.Id), CancellationToken.None);
        var foreign = await handler.Handle(new ListWorkspacePlanAssigneesQuery(Guid.NewGuid()), CancellationToken.None);

        listed.IsSuccess.Should().BeTrue();
        listed.Value.Should().ContainSingle(item => item.Email == "coach@example.com");
        foreign.Error.Code.Should().Be("license.not_found");
    }

    [Fact]
    public async Task Owner_RevokesSelectedSeatsAndRejectsSeatsFromAnotherPlan()
    {
        var actor = Actor();
        var subscription = Subscription.ForTenant(Guid.NewGuid(), actor.TenantId!.Value, Guid.NewGuid(), DateTime.UtcNow, 5);
        var otherSubscriptionId = Guid.NewGuid();
        var seat = SubscriptionSeat.Create(Guid.NewGuid(), subscription.Id, Guid.NewGuid(), actor.UserId, DateTime.UtcNow);
        var foreignSeat = SubscriptionSeat.Create(Guid.NewGuid(), otherSubscriptionId, Guid.NewGuid(), actor.UserId, DateTime.UtcNow);
        var store = Substitute.For<ILicenseAdminStore>();
        store.GetTenantSubscriptionAsync(actor.TenantId.Value, subscription.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        store.GetSeatAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(seat);
        store.GetSeatAsync(foreignSeat.Id, Arg.Any<CancellationToken>()).Returns(foreignSeat);
        store.SubscriptionTenantAsync(subscription.Id, Arg.Any<CancellationToken>()).Returns(actor.TenantId.Value);
        store.SubscriptionTenantAsync(otherSubscriptionId, Arg.Any<CancellationToken>()).Returns(actor.TenantId.Value);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        var cache = Substitute.For<IPermissionCacheInvalidator>();
        var handler = new RevokeWorkspacePlanSeatsCommandHandler(
            actor,
            Permissions(licenseManage: true),
            Tenants(actor),
            store,
            Substitute.For<IAuditLogRepository>(),
            cache,
            unitOfWork,
            Clock(),
            Substitute.For<IClientContext>());

        var revoked = await handler.Handle(new RevokeWorkspacePlanSeatsCommand(subscription.Id, [seat.Id]), CancellationToken.None);
        var rejected = await handler.Handle(new RevokeWorkspacePlanSeatsCommand(subscription.Id, [foreignSeat.Id]), CancellationToken.None);

        revoked.IsSuccess.Should().BeTrue();
        seat.IsActive.Should().BeFalse();
        cache.Received(1).ScheduleFeatures(actor.TenantId.Value);
        rejected.Error.Code.Should().Be("license.not_found");
        foreignSeat.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Owner_ListsPeopleWhoDoNotAlreadyHoldTheSelectedPlan()
    {
        var actor = Actor();
        var subscription = Subscription.ForTenant(Guid.NewGuid(), actor.TenantId!.Value, Guid.NewGuid(), DateTime.UtcNow, 5);
        var open = new TenantMemberOption(Guid.NewGuid(), "open@example.com", "Grace", "Hopper");
        var held = new TenantMemberOption(Guid.NewGuid(), "held@example.com", "Ada", "Lovelace");
        var store = Substitute.For<ILicenseAdminStore>();
        store.GetTenantSubscriptionAsync(actor.TenantId.Value, subscription.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        store.ListMembersAsync(actor.TenantId.Value, Arg.Any<CancellationToken>()).Returns(new[] { open, held });
        store.ActiveUserPlansAsync(actor.TenantId.Value, Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, UserPlanAssignment> { [open.UserId] = new(Guid.NewGuid(), "Starter") });
        store.ListActivePlanAssigneesAsync(actor.TenantId.Value, subscription.Id, Arg.Any<CancellationToken>()).Returns(new[] { new WorkspacePlanAssignee(Guid.NewGuid(), held.UserId, held.Email, held.FirstName, held.LastName, DateTime.UtcNow) });
        var handler = new ListWorkspacePlanCandidatesQueryHandler(actor, Permissions(licenseRead: true), Tenants(actor), store);

        var listed = await handler.Handle(new ListWorkspacePlanCandidatesQuery(subscription.Id), CancellationToken.None);

        listed.IsSuccess.Should().BeTrue();
        listed.Value.Should().ContainSingle(item => item.Email == "open@example.com" && item.CurrentPlan == "Starter");
    }

    [Fact]
    public async Task Owner_AssignsTheSelectedPlanToChosenMembers()
    {
        var actor = Actor();
        var userId = Guid.NewGuid();
        var subscription = Subscription.ForTenant(Guid.NewGuid(), actor.TenantId!.Value, Guid.NewGuid(), DateTime.UtcNow, 5);
        var store = Substitute.For<ILicenseAdminStore>();
        store.GetTenantSubscriptionAsync(actor.TenantId.Value, subscription.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        store.ActiveSeatsForUserAsync(actor.TenantId.Value, userId, Arg.Any<CancellationToken>()).Returns(Array.Empty<SubscriptionSeat>());
        store.ActiveSeatCountAsync(subscription.Id, Arg.Any<CancellationToken>()).Returns(1);
        store.ReserveSeatAsync(actor.TenantId.Value, subscription.Id, userId, actor.UserId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(SeatReservation.Ok(SubscriptionSeat.Create(Guid.NewGuid(), subscription.Id, userId, actor.UserId, DateTime.UtcNow)));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        var handler = new AssignWorkspacePlanUsersCommandHandler(actor, Permissions(licenseManage: true), Tenants(actor), Members(), store, Substitute.For<IAuditLogRepository>(), Substitute.For<IPermissionCacheInvalidator>(), unitOfWork, Clock(), Substitute.For<IClientContext>());

        var assigned = await handler.Handle(new AssignWorkspacePlanUsersCommand(subscription.Id, [userId]), CancellationToken.None);
        var outsider = Guid.NewGuid();
        var members = Substitute.For<ITenantMembershipRepository>();
        members.GetActiveAsync(actor.TenantId.Value, outsider, Arg.Any<CancellationToken>()).Returns((TenantMember?)null);
        var rejectedHandler = new AssignWorkspacePlanUsersCommandHandler(actor, Permissions(licenseManage: true), Tenants(actor), members, store, Substitute.For<IAuditLogRepository>(), Substitute.For<IPermissionCacheInvalidator>(), unitOfWork, Clock(), Substitute.For<IClientContext>());
        var rejected = await rejectedHandler.Handle(new AssignWorkspacePlanUsersCommand(subscription.Id, [outsider]), CancellationToken.None);

        assigned.IsSuccess.Should().BeTrue();
        await store.Received(1).ReserveSeatAsync(actor.TenantId.Value, subscription.Id, userId, actor.UserId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        rejected.Error.Code.Should().Be("license.not_found");
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
