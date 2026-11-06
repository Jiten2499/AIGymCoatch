using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Identity.Register;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Application.Tenancy;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Identity;

public sealed class StaffOnboardingTests
{
    [Fact]
    public async Task Wizard_InvitesNewStaff_WithoutReturningAPassword()
    {
        var harness = new IdentityHarness();
        AddRole(harness, RoleNames.Receptionist);
        var owner = await OpenGym(harness, "owner@gym.test", "Gym");
        ActAs(harness, owner);
        var branchId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var people = Substitute.For<IPeopleRepository>();
        people.EmployeeCodeExistsAsync(owner.TenantId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var branches = Substitute.For<IBranchRepository>();
        branches.GetAsync(owner.TenantId, branchId, Arg.Any<CancellationToken>()).Returns(Branch.Create(branchId, owner.TenantId, "Main", "MAIN", "UTC"));
        var licenses = Substitute.For<ILicenseAdminStore>();
        var subscription = Subscription.ForTenant(subscriptionId, owner.TenantId, Guid.NewGuid(), harness.Store.Clock.UtcNow, 5);
        licenses.GetTenantSubscriptionAsync(owner.TenantId, subscriptionId, Arg.Any<CancellationToken>()).Returns(subscription);
        licenses.ActiveSeatCountAsync(subscriptionId, Arg.Any<CancellationToken>()).Returns(0);
        licenses.ReserveSeatAsync(owner.TenantId, subscriptionId, Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call => SeatReservation.Ok(SubscriptionSeat.Create(Guid.NewGuid(), subscriptionId, call.ArgAt<Guid>(2), call.ArgAt<Guid?>(3), harness.Store.Clock.UtcNow)));
        StaffProfile? saved = null;
        people.When(item => item.AddStaffAsync(Arg.Any<StaffProfile>(), Arg.Any<CancellationToken>()))
            .Do(call => saved = call.Arg<StaffProfile>());

        var created = await Handler(harness, people, branches, licenses).Handle(
            new CreateWorkspaceUserCommand(
                "desk@gym.test",
                "Front",
                "Desk",
                RoleNames.Receptionist,
                Phone: "9999999999",
                Designation: "Receptionist",
                JoiningDate: DateOnly.FromDateTime(harness.Store.Clock.UtcNow),
                BranchId: branchId,
                EmploymentType: EmploymentType.FullTime,
                SubscriptionId: subscriptionId,
                SendInvitation: true),
            CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        created.Value.TemporaryPassword.Should().BeNull();
        created.Value.InvitationToken.Should().BeNull();
        created.Value.Status.Should().Be(nameof(ProfileStatus.PendingInvitation));
        saved.Should().NotBeNull();
        saved!.Designation.Should().Be("Receptionist");
    }

    [Fact]
    public async Task Wizard_RequiresLinkConfirmation_ForExistingUsers()
    {
        var harness = new IdentityHarness();
        AddRole(harness, RoleNames.Receptionist);
        var owner = await OpenGym(harness, "owner@gym.test", "Gym");
        await harness.Register().Handle(new RegisterCommand("desk@gym.test", "Correct-Horse-1", "Front", "Desk", null), CancellationToken.None);
        ActAs(harness, owner);

        var denied = await Handler(harness).Handle(
            new CreateWorkspaceUserCommand(
                "desk@gym.test",
                "Front",
                "Desk",
                RoleNames.Receptionist,
                Phone: "9999999999",
                Designation: "Receptionist",
                JoiningDate: DateOnly.FromDateTime(harness.Store.Clock.UtcNow),
                SendInvitation: true),
            CancellationToken.None);

        denied.IsFailure.Should().BeTrue();
        denied.Error.Code.Should().Be("staff.existing_user");
    }

    [Fact]
    public async Task Wizard_Draft_DoesNotInviteOrConsumeSeat()
    {
        var harness = new IdentityHarness();
        AddRole(harness, RoleNames.Accountant);
        var owner = await OpenGym(harness, "owner@gym.test", "Gym");
        ActAs(harness, owner);
        var people = Substitute.For<IPeopleRepository>();
        people.EmployeeCodeExistsAsync(owner.TenantId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var licenses = Substitute.For<ILicenseAdminStore>();
        StaffProfile? saved = null;
        people.When(item => item.AddStaffAsync(Arg.Any<StaffProfile>(), Arg.Any<CancellationToken>()))
            .Do(call => saved = call.Arg<StaffProfile>());

        var created = await Handler(harness, people, licenses: licenses).Handle(
            new CreateWorkspaceUserCommand(
                "books@gym.test",
                "Books",
                "Keeper",
                RoleNames.Accountant,
                Phone: "8888888888",
                Designation: "Accountant",
                JoiningDate: DateOnly.FromDateTime(harness.Store.Clock.UtcNow),
                SaveAsDraft: true),
            CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        created.Value.Status.Should().Be(nameof(ProfileStatus.Draft));
        created.Value.InvitationToken.Should().BeNull();
        saved!.Status.Should().Be(ProfileStatus.Draft);
        await licenses.DidNotReceiveWithAnyArgs().ReserveSeatAsync(default, default, default, default, default, default);
    }

    private static StaffOnboardingHandler Handler(
        IdentityHarness harness,
        IPeopleRepository? people = null,
        IBranchRepository? branches = null,
        ILicenseAdminStore? licenses = null)
    {
        return new(
            harness.Store.Actor,
            harness.Permissions,
            harness.Tenants,
            harness.Users,
            new AccountLinker(harness.Users, harness.Members, harness.Roles, harness.Passwords, harness.Policy, harness.UnitOfWork, harness.Store.Clock),
            harness.Members,
            harness.Roles,
            people ?? Substitute.For<IPeopleRepository>(),
            branches ?? Substitute.For<IBranchRepository>(),
            licenses ?? Substitute.For<ILicenseAdminStore>(),
            Substitute.For<IAccessRoleStore>(),
            Substitute.For<IPermissionCacheInvalidator>(),
            harness.Audit,
            harness.UnitOfWork,
            harness.Store.Clock,
            harness.Store.Client);
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
