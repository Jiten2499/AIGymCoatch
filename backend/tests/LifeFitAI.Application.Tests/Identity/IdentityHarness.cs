using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Identity.GetCurrentUser;
using LifeFitAI.Application.Identity.Login;
using LifeFitAI.Application.Identity.Password;
using LifeFitAI.Application.Identity.Refresh;
using LifeFitAI.Application.Identity.Register;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Tenancy;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Application.Tests.Identity;

internal sealed class IdentityHarness
{
    public IdentityHarness()
    {
        Store = new IdentityStore();
        Users = new FakeUsers(Store);
        Tenants = new FakeTenants(Store);
        Members = new FakeMembers(Store);
        Roles = new FakeRoles(Store);
        Plans = PersonalFreePlans.Create();
        Subscriptions = new FakeSubscriptions(Store);
        RefreshTokens = new FakeRefreshTokens(Store);
        Resets = new FakeResets(Store);
        Audit = new FakeAudit(Store);
        UnitOfWork = new FakeUnitOfWork(Store);
        Passwords = new Pbkdf2PasswordHasher(Options.Create(new PasswordOptions { Iterations = 1_000 }));
        Policy = new PasswordPolicy(Options.Create(new PasswordOptions { MinimumLength = 12 }));
        Secrets = new Sha256SecretTokenFactory();
        AccessTokens = new JwtAccessTokenFactory(
            Options.Create(new JwtOptions
            {
                SigningKey = "test-signing-key-at-least-32-bytes-long!!",
                Issuer = "LifeFitAI",
                Audience = "LifeFitAI",
                AccessTokenMinutes = 15,
            }),
            Store.Clock);
        Settings = new TokenSettings(Options.Create(new JwtOptions { RefreshTokenDays = 14, PasswordResetMinutes = 30 }));
        Sessions = new SessionIssuer(AccessTokens, Secrets, RefreshTokens, Store.Clock, Store.Client, UnitOfWork, Settings);
        Permissions = new PermissionService(Members, Roles);
        SubscriptionsService = new SubscriptionService(Tenants, Subscriptions, Store.Clock);
        Features = new FeatureFlagService(SubscriptionsService, Store.Actor, Store.Clock, new FakeEntitlementStore(Plans, Store));
        Seed();
    }

    public IdentityStore Store { get; }

    public FakeUsers Users { get; }

    public FakeTenants Tenants { get; }

    public FakeMembers Members { get; }

    public FakeRoles Roles { get; }

    public PersonalFreePlans Plans { get; }

    public FakeSubscriptions Subscriptions { get; }

    public FakeRefreshTokens RefreshTokens { get; }

    public FakeResets Resets { get; }

    public FakeAudit Audit { get; }

    public FakeUnitOfWork UnitOfWork { get; }

    public Pbkdf2PasswordHasher Passwords { get; }

    public PasswordPolicy Policy { get; }

    public Sha256SecretTokenFactory Secrets { get; }

    public JwtAccessTokenFactory AccessTokens { get; }

    public TokenSettings Settings { get; }

    public SessionIssuer Sessions { get; }

    public PermissionService Permissions { get; }

    public SubscriptionService SubscriptionsService { get; }

    public FeatureFlagService Features { get; }

    public RegisterCommandHandler Register() =>
        new(Users, Tenants, Members, Roles, Audit, UnitOfWork, Passwords, Sessions, Store.Clock, Store.Client);

    public LoginCommandHandler Login() =>
        new(Users, Tenants, Passwords, Sessions, Audit, UnitOfWork, Store.Clock, Store.Client);

    public RefreshCommandHandler Refresh() =>
        new(RefreshTokens, Secrets, Users, Tenants, Members, Sessions, UnitOfWork, Store.Clock, Store.Client, Store.Actor);

    public LogoutCommandHandler Logout() =>
        new(RefreshTokens, Secrets, Audit, UnitOfWork, Store.Clock, Store.Client);

    public ChangePasswordCommandHandler ChangePassword() =>
        new(Store.Actor, Users, Passwords, Policy, RefreshTokens, Audit, UnitOfWork, Store.Clock, Store.Client);

    public ForgotPasswordCommandHandler Forgot() =>
        new(Users, Resets, Secrets, Store.Notifier, Settings, UnitOfWork, Store.Clock);

    public ResetPasswordCommandHandler Reset() =>
        new(Resets, Users, Secrets, Passwords, RefreshTokens, Audit, UnitOfWork, Store.Clock, Store.Client);

    public GetCurrentUserQueryHandler CurrentUser() => new(Store.Actor, Users);

    public GetMyTenantsQueryHandler MyTenants() => new(Store.Actor, Members);

    public SwitchTenantCommandHandler Switch() =>
        new(Store.Actor, Members, Tenants, Users, Sessions, Audit, Store.Clock, Store.Client);

    public CreateTenantCommandHandler CreateTenant() =>
        new(Store.Actor, Permissions, Tenants, Members, Roles, Subscriptions, Plans, new DiscardProfiles(), Audit, UnitOfWork, Store.Clock, Store.Client);

    public AddTenantMemberCommandHandler AddMember() =>
        new(Store.Actor, Permissions, Members, Users, Roles, Audit, UnitOfWork, Store.Clock, Store.Client);

    public AssignRoleCommandHandler AssignRole() =>
        new(Store.Actor, Permissions, Members, Roles, Audit, UnitOfWork, Store.Clock, Store.Client);

    private void Seed()
    {
        // The single platform tenant hosts every self-registered normal user.
        var platform = Tenant.Create(PlatformConstants.PlatformTenantId, PlatformConstants.PlatformTenantName, TenantType.Platform);
        Store.Tenants.Add(platform);

        // Normal users inherit the platform tenant's PERSONAL_FREE subscription. Entitlements
        // are computed from the plan's features, never copied per subscription.
        var subscription = Subscription.ForTenant(Guid.NewGuid(), platform.Id, Plans.PersonalPlanId, Store.Clock.UtcNow, 0);
        Store.Subscriptions.Add(subscription);

        var personalMember = AddRole(RoleNames.PersonalMember);
        var personalOwner = AddRole(RoleNames.PersonalOwner);
        var owner = AddRole(RoleNames.GymOwner);
        var member = AddRole(RoleNames.Member);

        // PersonalMember owns only its own data. It may create a business workspace, but cannot
        // read or manage other platform members, so normal users never see each other.
        Grant(personalMember, PermissionCodes.UserRead, PermissionCodes.UserUpdate, PermissionCodes.TenantRead, PermissionCodes.TenantCreate, PermissionCodes.LicenseRead, PermissionCodes.FeatureRead);
        Grant(personalOwner, PermissionCodes.UserRead, PermissionCodes.UserUpdate, PermissionCodes.TenantRead, PermissionCodes.TenantUpdate, PermissionCodes.TenantCreate, PermissionCodes.TenantMemberRead, PermissionCodes.LicenseRead, PermissionCodes.FeatureRead);
        Grant(owner, PermissionCodes.UserRead, PermissionCodes.UserCreate, PermissionCodes.UserUpdate, PermissionCodes.UserDelete, PermissionCodes.TenantRead, PermissionCodes.TenantUpdate, PermissionCodes.TenantCreate, PermissionCodes.TenantMemberRead, PermissionCodes.TenantMemberInvite, PermissionCodes.TenantMemberRemove, PermissionCodes.RoleRead, PermissionCodes.RoleAssign, PermissionCodes.LicenseRead, PermissionCodes.FeatureRead);
        Grant(member, PermissionCodes.UserRead, PermissionCodes.TenantRead, PermissionCodes.FeatureRead);
    }

    private Role AddRole(string name)
    {
        var role = Role.Create(Guid.NewGuid(), name, name);
        Store.Roles.Add(role);
        return role;
    }

    private void Grant(Role role, params string[] codes)
    {
        foreach (var code in codes)
        {
            var permission = Store.Permissions.SingleOrDefault(item => item.Code == code);
            if (permission is null)
            {
                permission = Permission.Create(Guid.NewGuid(), code, code, code);
                Store.Permissions.Add(permission);
            }

            Store.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        }
    }
}

internal sealed class DiscardProfiles : LifeFitAI.Application.Gym.IBusinessProfileRepository
{
    public Task AddAsync(LifeFitAI.Domain.Gym.BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<LifeFitAI.Domain.Gym.BusinessProfile?> GetAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult<LifeFitAI.Domain.Gym.BusinessProfile?>(null);
}
