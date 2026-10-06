using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Identity.Register;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Tests.Identity;

public sealed class BusinessTenantAdminTests
{
    private const string Password = "Correct-Horse-1";

    [Fact]
    public async Task SuperAdmin_CreatesBusinessTenantAndReturnsOwnerPasswordOnce()
    {
        var harness = new IdentityHarness();
        var admin = await SignInSuperAdmin(harness);
        var handler = Handler(harness);

        var created = await handler.Handle(
            new CreateBusinessTenantCommand("North Gym", TenantType.Gym, "owner@northgym.test", "North", "Owner"),
            CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        created.Value.TemporaryPassword.Should().NotBeNullOrWhiteSpace();
        created.Value.OwnerEmail.Should().Be("owner@northgym.test");
        var owner = await harness.Users.GetByEmailAsync("owner@northgym.test", CancellationToken.None);
        owner.Should().NotBeNull();
        harness.Passwords.Verify(created.Value.TemporaryPassword!, owner!.PasswordHash, owner.PasswordSalt).Should().BeTrue();
        var roles = await harness.Roles.GetRoleNamesAsync(owner.Id, created.Value.Id, CancellationToken.None);
        roles.Should().Contain(RoleNames.GymOwner);
        admin.Id.Should().NotBe(owner.Id);
    }

    [Fact]
    public async Task SuperAdmin_CanEditAndCannotDeleteThePlatformTenant()
    {
        var harness = new IdentityHarness();
        await SignInSuperAdmin(harness);
        var handler = Handler(harness);
        var created = await handler.Handle(
            new CreateBusinessTenantCommand("North Gym", TenantType.Gym, "owner@northgym.test", "North", "Owner"),
            CancellationToken.None);

        var edited = await handler.Handle(
            new UpdateBusinessTenantCommand(created.Value.Id, "North Athletic", TenantType.CrossFit, TenantStatus.Suspended),
            CancellationToken.None);
        var updated = await harness.Tenants.GetAsync(created.Value.Id, CancellationToken.None);
        var deleted = await handler.Handle(new DeleteBusinessTenantsCommand([created.Value.Id]), CancellationToken.None);
        var platform = await handler.Handle(new DeleteBusinessTenantsCommand([PlatformConstants.PlatformTenantId]), CancellationToken.None);

        edited.IsSuccess.Should().BeTrue();
        updated!.Name.Should().Be("North Athletic");
        updated.TenantType.Should().Be(TenantType.CrossFit);
        updated.Status.Should().Be(TenantStatus.Suspended);
        deleted.IsSuccess.Should().BeTrue();
        (await harness.Tenants.GetAsync(created.Value.Id, CancellationToken.None)).Should().BeNull();
        platform.IsFailure.Should().BeTrue();
        platform.Error.Code.Should().Be("tenant.platform_protected");
    }

    private static TenantDirectoryHandler Handler(IdentityHarness harness) =>
        new(
            harness.Store.Actor,
            harness.Roles,
            harness.Permissions,
            harness.Users,
            new AccountLinker(harness.Users, harness.Members, harness.Roles, harness.Passwords, harness.Policy, harness.UnitOfWork, harness.Store.Clock),
            harness.Tenants,
            harness.Members,
            harness.Subscriptions,
            harness.Plans,
            new DiscardProfiles(),
            harness.Audit,
            harness.UnitOfWork,
            harness.Store.Clock,
            harness.Store.Client);

    private static async Task<UserAccount> SignInSuperAdmin(IdentityHarness harness)
    {
        var registered = await harness.Register().Handle(new RegisterCommand("admin@lifefit.test", Password, "Platform", "Admin", null), CancellationToken.None);
        var userId = registered.Value.User.Id;
        var role = Role.Create(Guid.NewGuid(), RoleNames.SuperAdmin, "Platform operator");
        var permission = Permission.Create(Guid.NewGuid(), PermissionCodes.PlatformAdmin, "Platform", null);
        harness.Store.Roles.Add(role);
        harness.Store.Permissions.Add(permission);
        harness.Store.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        await harness.Roles.AddAsync(UserRole.Create(Guid.NewGuid(), userId, role.Id, PlatformConstants.PlatformTenantId), CancellationToken.None);
        await harness.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        harness.Store.Actor.UserId = userId;
        harness.Store.Actor.TenantId = PlatformConstants.PlatformTenantId;
        return (await harness.Users.GetByIdAsync(userId, CancellationToken.None))!;
    }
}
