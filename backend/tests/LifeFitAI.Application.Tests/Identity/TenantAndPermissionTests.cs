using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Identity.Register;
using LifeFitAI.Application.Tenancy;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Application.Tests.Identity;

public sealed class TenantAndPermissionTests
{
    private const string Password = "Correct-Horse-1";

    [Fact]
    public async Task User_CanBelongToMultipleTenantsAndSwitchOnlyIntoMemberships()
    {
        var harness = new IdentityHarness();
        var registered = await harness.Register().Handle(new RegisterCommand("owner@lifefit.ai", Password, "Owner", "User", null), CancellationToken.None);
        harness.Store.Actor.UserId = registered.Value.User.Id;
        harness.Store.Actor.TenantId = registered.Value.CurrentTenant.Id;

        var gym = await harness.CreateTenant().Handle(new CreateTenantCommand("ABC Fitness", TenantType.Gym), CancellationToken.None);
        var workspaces = await harness.MyTenants().Handle(new GetMyTenantsQuery(), CancellationToken.None);
        var switched = await harness.Switch().Handle(new SwitchTenantCommand(gym.Value.Id), CancellationToken.None);
        var outsiderTenant = Guid.NewGuid();
        var denied = await harness.Switch().Handle(new SwitchTenantCommand(outsiderTenant), CancellationToken.None);
        var spoofed = await harness.Permissions.HasPermissionAsync(registered.Value.User.Id, outsiderTenant, PermissionCodes.TenantRead, CancellationToken.None);

        gym.IsSuccess.Should().BeTrue();
        gym.Value.Type.Should().Be(TenantType.Gym);
        workspaces.Value.Should().HaveCount(2);
        switched.IsSuccess.Should().BeTrue();
        switched.Value.CurrentTenant.Id.Should().Be(gym.Value.Id);
        ReadTenant(switched.Value.AccessToken).Should().Be(gym.Value.Id);
        denied.Error.Should().Be(IdentityErrors.NotMember);
        spoofed.Should().BeFalse();
        var gymRoles = await harness.Roles.GetRoleNamesAsync(registered.Value.User.Id, gym.Value.Id, CancellationToken.None);
        gymRoles.Should().Contain(RoleNames.GymOwner);
    }

    [Fact]
    public async Task Permissions_AreTenantScoped()
    {
        var harness = new IdentityHarness();
        var owner = await harness.Register().Handle(new RegisterCommand("owner@lifefit.ai", Password, "Owner", "User", null), CancellationToken.None);
        var member = await harness.Register().Handle(new RegisterCommand("member@lifefit.ai", Password, "Member", "User", null), CancellationToken.None);
        harness.Store.Actor.UserId = owner.Value.User.Id;
        harness.Store.Actor.TenantId = owner.Value.CurrentTenant.Id;
        var gym = await harness.CreateTenant().Handle(new CreateTenantCommand("ABC Fitness", TenantType.Gym), CancellationToken.None);
        var added = await harness.AddMember().Handle(new AddTenantMemberCommand(gym.Value.Id, "member@lifefit.ai", RoleNames.Member), CancellationToken.None);

        var ownerCanDelete = await harness.Permissions.HasPermissionAsync(owner.Value.User.Id, gym.Value.Id, PermissionCodes.UserDelete, CancellationToken.None);
        var memberCanDelete = await harness.Permissions.HasPermissionAsync(member.Value.User.Id, gym.Value.Id, PermissionCodes.UserDelete, CancellationToken.None);
        var ownerPersonalDelete = await harness.Permissions.HasPermissionAsync(owner.Value.User.Id, owner.Value.CurrentTenant.Id, PermissionCodes.UserDelete, CancellationToken.None);
        var superAdmin = await harness.AssignRole().Handle(new Application.Authorization.AssignRoleCommand(gym.Value.Id, member.Value.User.Id, RoleNames.SuperAdmin), CancellationToken.None);

        added.IsSuccess.Should().BeTrue();
        ownerCanDelete.Should().BeTrue();
        memberCanDelete.Should().BeFalse();
        ownerPersonalDelete.Should().BeFalse();
        superAdmin.Error.Should().Be(IdentityErrors.SuperAdminRejected);
    }

    [Fact]
    public async Task CreateTenant_RejectsNonBusinessType()
    {
        var harness = new IdentityHarness();
        var registered = await harness.Register().Handle(new RegisterCommand("owner@lifefit.ai", Password, "Owner", "User", null), CancellationToken.None);
        harness.Store.Actor.UserId = registered.Value.User.Id;
        harness.Store.Actor.TenantId = registered.Value.CurrentTenant.Id;

        var result = await harness.CreateTenant().Handle(new CreateTenantCommand("Home", TenantType.Platform), CancellationToken.None);

        result.Error.Should().Be(IdentityErrors.PersonalTypeRejected);
    }

    private static Guid ReadTenant(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return Guid.Parse(jwt.Claims.Single(claim => claim.Type == "tenant_id").Value);
    }
}

public sealed class LicensingTests
{
    private const string Password = "Correct-Horse-1";

    [Fact]
    public async Task Feature_RequiresActiveTenantAndUsableSubscription()
    {
        var harness = new IdentityHarness();
        var registered = await harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);
        var tenantId = registered.Value.CurrentTenant.Id;

        // AiCoach is enabled by the PERSONAL_FREE plan, so it is live while the subscription is usable.
        var enabled = await harness.Features.IsEnabledAsync(tenantId, FeatureCodes.AiCoach, CancellationToken.None);

        harness.Store.Subscriptions.Single().SetExpiresAt(harness.Store.Clock.UtcNow.AddMinutes(-1));
        var expired = await harness.Features.IsEnabledAsync(tenantId, FeatureCodes.AiCoach, CancellationToken.None);

        harness.Store.Subscriptions.Single().SetExpiresAt(null);
        harness.Store.Subscriptions.Single().Suspend();
        var suspended = await harness.Features.IsEnabledAsync(tenantId, FeatureCodes.AiCoach, CancellationToken.None);

        harness.Store.Subscriptions.Single().MarkExpired(harness.Store.Clock.UtcNow);
        var marked = await harness.Features.IsEnabledAsync(tenantId, FeatureCodes.AiCoach, CancellationToken.None);

        enabled.Should().BeTrue();
        expired.Should().BeFalse();
        suspended.Should().BeFalse();
        marked.Should().BeFalse();
    }

    [Fact]
    public async Task Feature_NotOnPlanIsDisabled()
    {
        var harness = new IdentityHarness();
        var registered = await harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);

        // MemberManagement is a business feature and is not part of the PERSONAL_FREE plan.
        var enabled = await harness.Features.IsEnabledAsync(registered.Value.CurrentTenant.Id, FeatureCodes.MemberManagement, CancellationToken.None);

        enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Feature_IsDisabledWhenTenantIsSuspended()
    {
        var harness = new IdentityHarness();
        var registered = await harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);
        harness.Store.Tenants.Single(tenant => tenant.Id == registered.Value.CurrentTenant.Id).Suspend();

        var enabled = await harness.Features.IsEnabledAsync(registered.Value.CurrentTenant.Id, FeatureCodes.WorkoutPlans, CancellationToken.None);

        enabled.Should().BeFalse();
    }

    [Fact]
    public void PasswordHasher_UsesSaltAndRejectsWrongPassword()
    {
        var hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordOptions { Iterations = 1_000 }));
        var (hash, salt) = hasher.HashPassword("Correct-Horse-1");

        hash.Should().NotContain("Correct-Horse-1");
        salt.Should().NotBe(hash);
        hasher.Verify("Correct-Horse-1", hash, salt).Should().BeTrue();
        hasher.Verify("wrong", hash, salt).Should().BeFalse();
    }
}
