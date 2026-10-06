using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Licensing;

public sealed class PlanAssignmentTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _actorTenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AssignTenantPlan_RequiresSuperAdmin()
    {
        var store = Substitute.For<ILicenseAdminStore>();
        var result = await TenantHandler(store, superAdmin: false).Handle(new AssignTenantPlanCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be("authorization.forbidden");
        await store.DidNotReceive().GetPlanAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignTenantPlan_ChangesTheBusinessPlanAndRejectsAPersonalPlanOrTooFewSeats()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Pro ultimate", TenantType.Gym);
        var small = ActivePlan("BASIC", "Business", 1);
        var plan = ActivePlan("PRO", "Business", 5);
        var personal = ActivePlan("PERSONAL", "Personal", 1);
        var current = Subscription.ForTenant(Guid.NewGuid(), tenant.Id, Guid.NewGuid(), _now, 10);
        var store = Substitute.For<ILicenseAdminStore>();
        var tenants = Substitute.For<ITenantRepository>();
        tenants.GetAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        store.GetActiveSubscriptionAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(current);
        store.GetPlanAsync(small.Id, Arg.Any<CancellationToken>()).Returns(small);
        store.GetPlanAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        store.GetPlanAsync(personal.Id, Arg.Any<CancellationToken>()).Returns(personal);
        store.ActiveSeatCountAsync(current.Id, Arg.Any<CancellationToken>()).Returns(2);

        var tooSmall = await TenantHandler(store, tenants).Handle(new AssignTenantPlanCommand(tenant.Id, small.Id), CancellationToken.None);
        tooSmall.Error.Code.Should().Be("license.capacity_conflict");
        current.PlanId.Should().NotBe(small.Id);

        store.ActiveSeatCountAsync(current.Id, Arg.Any<CancellationToken>()).Returns(1);
        var assigned = await TenantHandler(store, tenants).Handle(new AssignTenantPlanCommand(tenant.Id, plan.Id), CancellationToken.None);
        assigned.IsSuccess.Should().BeTrue();
        current.PlanId.Should().Be(plan.Id);
        current.Seats.Should().Be(5);

        var rejected = await TenantHandler(store, tenants).Handle(new AssignTenantPlanCommand(tenant.Id, personal.Id), CancellationToken.None);
        rejected.Error.Code.Should().Be("license.plan_missing");
    }

    [Fact]
    public async Task AssignUserPlan_CreatesAPersonalSubscriptionAndRejectsABusinessPlan()
    {
        var user = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "ada@gym.test", null, "hash", "salt", "Ada", "Lovelace");
        var personal = ActivePlan("PERSONAL", "Personal", 1);
        var business = ActivePlan("PRO", "Business", 10);
        var users = Substitute.For<IUserAccountRepository>();
        var store = Substitute.For<ILicenseAdminStore>();
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        store.GetPlanAsync(personal.Id, Arg.Any<CancellationToken>()).Returns(personal);
        store.GetPlanAsync(business.Id, Arg.Any<CancellationToken>()).Returns(business);
        store.GetActiveUserSubscriptionAsync(user.Id, Arg.Any<CancellationToken>()).Returns((Subscription?)null);

        var assigned = await UserHandler(store, users).Handle(new AssignUserPlanCommand(user.Id, personal.Id), CancellationToken.None);
        assigned.IsSuccess.Should().BeTrue();
        await store.Received(1).AddSubscriptionAsync(Arg.Is<Subscription>(subscription => subscription.UserId == user.Id && subscription.PlanId == personal.Id), Arg.Any<CancellationToken>());

        var rejected = await UserHandler(store, users).Handle(new AssignUserPlanCommand(user.Id, business.Id), CancellationToken.None);
        rejected.Error.Code.Should().Be("license.plan_missing");
    }

    private AssignTenantPlanCommandHandler TenantHandler(ILicenseAdminStore store, ITenantRepository? tenants = null, bool superAdmin = true) =>
        new(Actor(), Roles(superAdmin), Permissions(superAdmin), tenants ?? Substitute.For<ITenantRepository>(), store, Substitute.For<IAuditLogRepository>(), Substitute.For<IPermissionCacheInvalidator>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

    private AssignUserPlanCommandHandler UserHandler(ILicenseAdminStore store, IUserAccountRepository users) =>
        new(Actor(), Roles(true), Permissions(true), users, store, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_actorId);
        actor.TenantId.Returns(_actorTenantId);
        return actor;
    }

    private IRoleRepository Roles(bool superAdmin)
    {
        var roles = Substitute.For<IRoleRepository>();
        roles.GetRoleNamesAsync(_actorId, _actorTenantId, Arg.Any<CancellationToken>()).Returns(superAdmin ? new[] { RoleNames.SuperAdmin } : new[] { RoleNames.GymOwner });
        return roles;
    }

    private IPermissionService Permissions(bool allowed)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_actorId, _actorTenantId, PermissionCodes.PlatformAdmin, Arg.Any<CancellationToken>()).Returns(allowed);
        return permissions;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_now);
        return clock;
    }

    private static PlanDefinition ActivePlan(string code, string appliesTo, int seats)
    {
        var plan = PlanDefinition.Create(Guid.NewGuid(), code, code, null, appliesTo, 1, seats);
        plan.Activate();
        return plan;
    }
}
