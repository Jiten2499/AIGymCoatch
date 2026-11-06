using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.SharedKernel.Results;
using LifeFitAI.Application.Identity.Register;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Application.Tenancy;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Identity;

public sealed class WorkspaceUserTests
{
    [Fact]
    public async Task Owner_CreatesUserWithPassword_AndDoesNotSeeAnotherWorkspace()
    {
        var harness = new IdentityHarness();
        AddRole(harness, RoleNames.Trainer);
        var owner = await OpenGym(harness, "north@gym.test", "North Gym");
        var other = await OpenGym(harness, "south@gym.test", "South Gym");
        ActAs(harness, other);
        await Create(harness).Handle(new CreateWorkspaceUserCommand("coach@south.test", "South", "Coach", RoleNames.Trainer), CancellationToken.None);
        ActAs(harness, owner);

        var created = await Create(harness).Handle(new CreateWorkspaceUserCommand("coach@north.test", "North", "Coach", RoleNames.Trainer), CancellationToken.None);
        var listed = await List(harness).Handle(new ListWorkspaceUsersQuery(1, 20), CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        created.Value.TemporaryPassword.Should().BeNull();
        var account = await harness.Users.GetByEmailAsync("coach@north.test", CancellationToken.None);
        account!.PasswordHash.Should().NotBeNullOrWhiteSpace();
        (await harness.Roles.GetRoleNamesAsync(account.Id, owner.TenantId, CancellationToken.None)).Should().Contain(RoleNames.Trainer);
        listed.Value.Items.Select(item => item.Email).Should().Contain(["north@gym.test", "coach@north.test"]);
        listed.Value.Items.Select(item => item.Email).Should().NotContain("coach@south.test");
        listed.Value.Items.Single(item => item.Email == "north@gym.test").IsOwner.Should().BeTrue();
    }

    [Fact]
    public async Task Owner_LinksExistingAccountWithoutANewPassword()
    {
        var harness = new IdentityHarness();
        AddRole(harness, RoleNames.Receptionist);
        var owner = await OpenGym(harness, "owner@gym.test", "Gym");
        await harness.Register().Handle(new RegisterCommand("desk@gym.test", "Correct-Horse-1", "Front", "Desk", null), CancellationToken.None);
        ActAs(harness, owner);

        var created = await Create(harness).Handle(new CreateWorkspaceUserCommand("desk@gym.test", "Front", "Desk", RoleNames.Receptionist), CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        created.Value.TemporaryPassword.Should().BeNull();
        (await harness.Members.GetActiveAsync(owner.TenantId, created.Value.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task Owner_SeesStaffWithCustomRoles_InTheWorkspaceList()
    {
        var harness = new IdentityHarness();
        var owner = await OpenGym(harness, "owner@gym.test", "Gym");
        ActAs(harness, owner);
        var custom = Role.CreateCustom(Guid.NewGuid(), owner.TenantId, "Front desk lead", "Custom desk");
        harness.Store.Roles.Add(custom);

        var created = await Create(harness).Handle(new CreateWorkspaceUserCommand("lead@gym.test", "Front", "Lead", custom.Name), CancellationToken.None);
        var listed = await List(harness).Handle(new ListWorkspaceUsersQuery(1, 20), CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        listed.Value.Items.Select(item => item.Email).Should().Contain("lead@gym.test");
        listed.Value.Items.Single(item => item.Email == "lead@gym.test").Role.Should().Be(custom.Name);
    }

    [Fact]
    public async Task Owner_ListsCustomRoleStaff_WhenACoachRoleIsAlsoAssigned()
    {
        var harness = new IdentityHarness();
        AddRole(harness, RoleNames.Trainer);
        var owner = await OpenGym(harness, "owner@gym.test", "Gym");
        ActAs(harness, owner);
        var custom = Role.CreateCustom(Guid.NewGuid(), owner.TenantId, "Front desk lead", "Custom desk");
        harness.Store.Roles.Add(custom);

        var created = await Create(harness).Handle(new CreateWorkspaceUserCommand("lead@gym.test", "Front", "Lead", custom.Name), CancellationToken.None);
        var trainer = harness.Store.Roles.Single(role => role.Name == RoleNames.Trainer);
        await harness.Roles.AddAsync(UserRole.Create(Guid.NewGuid(), created.Value.Id, trainer.Id, owner.TenantId), CancellationToken.None);
        var listed = await List(harness).Handle(new ListWorkspaceUsersQuery(1, 20), CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        listed.Value.Items.Select(item => item.Email).Should().Contain("lead@gym.test");
        listed.Value.Items.Single(item => item.Email == "lead@gym.test").Role.Should().Be(custom.Name);
    }

    [Fact]
    public async Task Owner_EditsRoleAndCannotDeleteOrDemoteTheOwner()
    {
        var harness = new IdentityHarness();
        AddRole(harness, RoleNames.Trainer);
        AddRole(harness, RoleNames.Receptionist);
        var owner = await OpenGym(harness, "owner@gym.test", "Gym");
        ActAs(harness, owner);
        var created = await Create(harness).Handle(new CreateWorkspaceUserCommand("coach@gym.test", "Gym", "Coach", RoleNames.Trainer), CancellationToken.None);

        var edited = await Update(harness).Handle(new UpdateWorkspaceUserCommand(created.Value.Id, "Gym", "Lead", UserStatus.Active, RoleNames.Receptionist), CancellationToken.None);
        edited.IsSuccess.Should().BeTrue();
        (await harness.Users.GetByIdAsync(created.Value.Id, CancellationToken.None))!.LastName.Should().Be("Lead");
        (await harness.Roles.GetRoleNamesAsync(created.Value.Id, owner.TenantId, CancellationToken.None)).Should().Contain(RoleNames.Receptionist);

        var demoted = await Update(harness).Handle(new UpdateWorkspaceUserCommand(owner.UserId, "Owner", "Person", UserStatus.Active, RoleNames.Member), CancellationToken.None);
        var removed = await Manage(harness).Handle(new DeleteWorkspaceUsersCommand([owner.UserId]), CancellationToken.None);
        var deleted = await Manage(harness).Handle(new DeleteWorkspaceUsersCommand([created.Value.Id]), CancellationToken.None);

        demoted.Error.Code.Should().Be(IdentityErrors.OwnerRoleRejected.Code);
        removed.Error.Code.Should().Be(IdentityErrors.OwnerRemovalRejected.Code);
        deleted.IsSuccess.Should().BeTrue();
        (await harness.Members.GetActiveAsync(owner.TenantId, created.Value.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Member_CannotCreateUsers_AndPlatformWorkspaceIsRejected()
    {
        var harness = new IdentityHarness();
        var owner = await OpenGym(harness, "owner@gym.test", "Gym");
        ActAs(harness, owner);
        var created = await Create(harness).Handle(new CreateWorkspaceUserCommand("member@gym.test", "Gym", "Member", RoleNames.Member), CancellationToken.None);
        var member = await harness.Users.GetByIdAsync(created.Value.Id, CancellationToken.None);
        harness.Store.Actor.UserId = member!.Id;

        var denied = await Create(harness).Handle(new CreateWorkspaceUserCommand("other@gym.test", "Other", "Person", RoleNames.Member), CancellationToken.None);
        harness.Store.Actor.UserId = owner.UserId;
        harness.Store.Actor.TenantId = PlatformConstants.PlatformTenantId;
        var platform = await Create(harness).Handle(new CreateWorkspaceUserCommand("other@gym.test", "Other", "Person", RoleNames.Member), CancellationToken.None);
        harness.Store.Actor.TenantId = owner.TenantId;
        var superAdmin = await Create(harness).Handle(new CreateWorkspaceUserCommand("root@gym.test", "Root", "User", RoleNames.SuperAdmin), CancellationToken.None);

        denied.Error.Code.Should().Be(IdentityErrors.Forbidden.Code);
        platform.Error.Code.Should().Be(IdentityErrors.PlatformWorkspaceRejected.Code);
        superAdmin.Error.Code.Should().Be(IdentityErrors.SuperAdminRejected.Code);
    }

    private static StaffOnboardingHandler Create(IdentityHarness harness)
    {
        var people = Substitute.For<IPeopleRepository>();
        var branches = Substitute.For<IBranchRepository>();
        var licenses = Substitute.For<ILicenseAdminStore>();
        var accessRoles = Substitute.For<IAccessRoleStore>();
        var cache = Substitute.For<IPermissionCacheInvalidator>();
        return new(
            harness.Store.Actor,
            harness.Permissions,
            harness.Tenants,
            harness.Users,
            new AccountLinker(harness.Users, harness.Members, harness.Roles, harness.Passwords, harness.Policy, harness.UnitOfWork, harness.Store.Clock),
            harness.Members,
            harness.Roles,
            people,
            branches,
            licenses,
            accessRoles,
            cache,
            harness.Audit,
            harness.UnitOfWork,
            harness.Store.Clock,
            harness.Store.Client);
    }

    private static StaffUpdateHandler Update(IdentityHarness harness)
    {
        var people = Substitute.For<IPeopleRepository>();
        var branches = Substitute.For<IBranchRepository>();
        var licenses = Substitute.For<ILicenseAdminStore>();
        licenses.ActiveSeatsForUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var accessRoles = Substitute.For<IAccessRoleStore>();
        var cache = Substitute.For<IPermissionCacheInvalidator>();
        return new(
            harness.Store.Actor,
            harness.Permissions,
            harness.Tenants,
            harness.Users,
            harness.Members,
            harness.Roles,
            people,
            branches,
            licenses,
            accessRoles,
            cache,
            harness.Audit,
            harness.UnitOfWork,
            harness.Store.Clock,
            harness.Store.Client);
    }

    private static WorkspaceUserCommandHandler Manage(IdentityHarness harness)
    {
        var profiles = Substitute.For<IBusinessProfileSoftDelete>();
        profiles.DeleteStaffUsersAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var tenantId = call.ArgAt<Guid>(0);
                var userIds = call.ArgAt<IReadOnlyList<Guid>>(2);
                var now = call.ArgAt<DateTime>(3);
                foreach (var userId in userIds)
                {
                    var member = harness.Members.GetActiveAsync(tenantId, userId, CancellationToken.None).GetAwaiter().GetResult();
                    member?.Remove(now);
                    harness.Roles.RemoveForMemberAsync(userId, tenantId, now, CancellationToken.None).GetAwaiter().GetResult();
                }

                return Task.FromResult(Result.Success());
            });
        return new(
            harness.Store.Actor,
            harness.Permissions,
            harness.Tenants,
            harness.Users,
            new AccountLinker(harness.Users, harness.Members, harness.Roles, harness.Passwords, harness.Policy, harness.UnitOfWork, harness.Store.Clock),
            harness.Members,
            harness.Roles,
            harness.Audit,
            harness.UnitOfWork,
            harness.Store.Clock,
            harness.Store.Client,
            profiles);
    }

    private static ListWorkspaceUsersQueryHandler List(IdentityHarness harness)
    {
        var licenses = Substitute.For<ILicenseAdminStore>();
        licenses.ActiveUserPlansAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, UserPlanAssignment>());
        return new(harness.Store.Actor, harness.Permissions, harness.Tenants, harness.Users, harness.Roles, licenses);
    }

    private static async Task<(Guid UserId, Guid TenantId)> OpenGym(IdentityHarness harness, string email, string name)
    {
        var registered = await harness.Register().Handle(new RegisterCommand("admin@lifefit.test", "Correct-Horse-1", "Platform", "Admin", null), CancellationToken.None);
        var adminId = registered.IsSuccess
            ? registered.Value.User.Id
            : (await harness.Users.GetByEmailAsync("admin@lifefit.test", CancellationToken.None))!.Id;
        var superAdmin = harness.Store.Roles.SingleOrDefault(role => role.Name == RoleNames.SuperAdmin);
        if (superAdmin is null)
        {
            superAdmin = Role.Create(Guid.NewGuid(), RoleNames.SuperAdmin, "Platform operator");
            harness.Store.Roles.Add(superAdmin);
            var permission = Permission.Create(Guid.NewGuid(), PermissionCodes.PlatformAdmin, "Platform", null);
            harness.Store.Permissions.Add(permission);
            harness.Store.RolePermissions.Add(RolePermission.Create(superAdmin.Id, permission.Id));
        }

        if (!harness.Store.UserRoles.Any(role => role.UserId == adminId && role.RoleId == superAdmin.Id && role.DeletedAt == null))
        {
            await harness.Roles.AddAsync(UserRole.Create(Guid.NewGuid(), adminId, superAdmin.Id, PlatformConstants.PlatformTenantId), CancellationToken.None);
            await harness.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        harness.Store.Actor.UserId = adminId;
        harness.Store.Actor.TenantId = PlatformConstants.PlatformTenantId;
        var created = await new TenantDirectoryHandler(
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
            harness.Store.Client).Handle(new CreateBusinessTenantCommand(name, TenantType.Gym, email, "Gym", "Owner"), CancellationToken.None);
        var owner = await harness.Users.GetByEmailAsync(email, CancellationToken.None);
        return (owner!.Id, created.Value.Id);
    }

    private static void ActAs(IdentityHarness harness, (Guid UserId, Guid TenantId) workspace)
    {
        harness.Store.Actor.UserId = workspace.UserId;
        harness.Store.Actor.TenantId = workspace.TenantId;
    }

    private static void AddRole(IdentityHarness harness, string name)
    {
        if (harness.Store.Roles.All(role => role.Name != name))
        {
            harness.Store.Roles.Add(Role.Create(Guid.NewGuid(), name, name));
        }
    }
}
