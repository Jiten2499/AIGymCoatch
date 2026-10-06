using FluentAssertions;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Identity.Login;
using LifeFitAI.Application.Identity.Password;
using LifeFitAI.Application.Identity.Refresh;
using LifeFitAI.Application.Identity.Register;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Tests.Identity;

public sealed class RegistrationTests
{
    private const string Password = "Correct-Horse-1";

    [Fact]
    public async Task Register_AttachesNormalUserToThePlatformTenant()
    {
        var harness = new IdentityHarness();

        var result = await harness.Register().Handle(
            new RegisterCommand("Jitender@LifeFit.ai", Password, "Jitender", "Singh", null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Email.Should().Be("jitender@lifefit.ai");
        result.Value.CurrentTenant.Id.Should().Be(PlatformConstants.PlatformTenantId);
        result.Value.CurrentTenant.Type.Should().Be(TenantType.Platform);
        harness.Store.Users.Should().ContainSingle();
        harness.Store.Users[0].TenantId.Should().Be(PlatformConstants.PlatformTenantId);
        harness.Store.Users[0].PasswordHash.Should().NotBe(Password);
        harness.Store.Users[0].PasswordSalt.Should().NotBeNullOrWhiteSpace();
        harness.Store.Members.Should().ContainSingle(member => member.UserId == result.Value.User.Id && member.TenantId == PlatformConstants.PlatformTenantId);
        var roles = await harness.Roles.GetRoleNamesAsync(result.Value.User.Id, PlatformConstants.PlatformTenantId, CancellationToken.None);
        roles.Should().ContainSingle().Which.Should().Be(RoleNames.PersonalMember);

        // A normal user does not create a tenant or a subscription; only the seeded platform rows exist.
        harness.Store.Tenants.Should().ContainSingle(tenant => tenant.Id == PlatformConstants.PlatformTenantId);
        harness.Store.Subscriptions.Should().ContainSingle(subscription => subscription.TenantId == PlatformConstants.PlatformTenantId);
        harness.Store.Audits.Should().Contain(entry => entry.Action == "Register");
        harness.Store.RefreshTokens.Single().TokenHash.Should().NotBe(result.Value.RefreshToken);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail()
    {
        var harness = new IdentityHarness();
        await harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);

        var result = await harness.Register().Handle(new RegisterCommand("A@lifefit.ai", Password, "B", "User", null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(IdentityErrors.EmailConflict);
        harness.Store.Users.Should().ContainSingle();
    }

    [Fact]
    public async Task Register_RollsBackWhenSetupFails()
    {
        var harness = new IdentityHarness();
        harness.Store.FailNextAudit = true;

        var act = () => harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        harness.Store.Users.Should().BeEmpty();
        harness.Store.Members.Should().BeEmpty();
        harness.Store.RefreshTokens.Should().BeEmpty();
    }

    [Fact]
    public void Register_RejectsWeakPassword()
    {
        var harness = new IdentityHarness();
        var validator = new RegisterCommandValidator(harness.Policy);

        var result = validator.Validate(new RegisterCommand("a@lifefit.ai", "short", "A", "User", null));

        result.IsValid.Should().BeFalse();
    }
}

public sealed class LoginAndSessionTests
{
    private const string Password = "Correct-Horse-1";

    [Fact]
    public async Task Login_SucceedsAndRefreshRotates()
    {
        var harness = new IdentityHarness();
        var registered = await harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);
        harness.Store.Actor.UserId = registered.Value.User.Id;
        harness.Store.Actor.TenantId = registered.Value.CurrentTenant.Id;

        var login = await harness.Login().Handle(new LoginCommand("a@lifefit.ai", Password), CancellationToken.None);
        var rotated = await harness.Refresh().Handle(new RefreshCommand(login.Value.RefreshToken), CancellationToken.None);
        var replay = await harness.Refresh().Handle(new RefreshCommand(login.Value.RefreshToken), CancellationToken.None);

        login.IsSuccess.Should().BeTrue();
        harness.Store.Users.Single().LastLoginAt.Should().NotBeNull();
        rotated.IsSuccess.Should().BeTrue();
        rotated.Value.RefreshToken.Should().NotBe(login.Value.RefreshToken);
        var original = harness.Store.RefreshTokens.Single(token => token.TokenHash == harness.Secrets.Hash(login.Value.RefreshToken));
        original.RevokedAt.Should().NotBeNull();
        original.ReplacedByTokenId.Should().Be(harness.Store.RefreshTokens.Single(token => token.TokenHash == harness.Secrets.Hash(rotated.Value.RefreshToken)).Id);
        replay.IsSuccess.Should().BeFalse();
        harness.Store.RefreshTokens.Where(token => token.RevokedAt == null).Should().BeEmpty();
    }

    [Fact]
    public async Task Login_RejectsInvalidAndDisabledUsers()
    {
        var harness = new IdentityHarness();
        await harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);

        var invalid = await harness.Login().Handle(new LoginCommand("a@lifefit.ai", "Wrong-Password-1"), CancellationToken.None);
        harness.Store.Users.Single().Disable();
        var disabled = await harness.Login().Handle(new LoginCommand("a@lifefit.ai", Password), CancellationToken.None);

        invalid.Error.Should().Be(IdentityErrors.InvalidCredentials);
        disabled.Error.Should().Be(IdentityErrors.UserDisabled);
    }

    [Fact]
    public async Task Refresh_ExpiredTokenDoesNotRevokeTheFamily()
    {
        var harness = new IdentityHarness();
        var login = await RegisterAndLogin(harness);

        harness.Store.Clock.UtcNow = harness.Store.Clock.UtcNow.AddDays(15);
        var result = await harness.Refresh().Handle(new RefreshCommand(login.RefreshToken), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        harness.Store.RefreshTokens.Single(token => token.TokenHash == harness.Secrets.Hash(login.RefreshToken)).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var harness = new IdentityHarness();
        var login = await RegisterAndLogin(harness);

        var logout = await harness.Logout().Handle(new LogoutCommand(login.RefreshToken), CancellationToken.None);
        var refresh = await harness.Refresh().Handle(new RefreshCommand(login.RefreshToken), CancellationToken.None);

        logout.IsSuccess.Should().BeTrue();
        refresh.IsSuccess.Should().BeFalse();
        harness.Store.Audits.Should().Contain(entry => entry.Action == "Logout");
    }

    [Fact]
    public async Task ChangePassword_ReplacesSecretAndRevokesRefreshTokens()
    {
        var harness = new IdentityHarness();
        var registered = await harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);
        var login = await harness.Login().Handle(new LoginCommand("a@lifefit.ai", Password), CancellationToken.None);
        harness.Store.Actor.UserId = registered.Value.User.Id;
        harness.Store.Actor.TenantId = registered.Value.CurrentTenant.Id;

        var changed = await harness.ChangePassword().Handle(new ChangePasswordCommand(Password, "Next-Secret-22"), CancellationToken.None);
        var oldRefresh = await harness.Refresh().Handle(new RefreshCommand(login.Value.RefreshToken), CancellationToken.None);
        var oldLogin = await harness.Login().Handle(new LoginCommand("a@lifefit.ai", Password), CancellationToken.None);
        var newLogin = await harness.Login().Handle(new LoginCommand("a@lifefit.ai", "Next-Secret-22"), CancellationToken.None);

        changed.IsSuccess.Should().BeTrue();
        oldRefresh.IsSuccess.Should().BeFalse();
        oldLogin.IsSuccess.Should().BeFalse();
        newLogin.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_UsesNotifierTokenAndDoesNotReturnIt()
    {
        var harness = new IdentityHarness();
        await harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);

        var forgot = await harness.Forgot().Handle(new ForgotPasswordCommand("a@lifefit.ai"), CancellationToken.None);
        var token = harness.Store.Notifier.Sent.Single().Token;
        var reset = await harness.Reset().Handle(new ResetPasswordCommand(token, "Reset-Secret-22"), CancellationToken.None);
        var login = await harness.Login().Handle(new LoginCommand("a@lifefit.ai", "Reset-Secret-22"), CancellationToken.None);

        forgot.IsSuccess.Should().BeTrue();
        reset.IsSuccess.Should().BeTrue();
        login.IsSuccess.Should().BeTrue();
        harness.Store.Resets.Single().TokenHash.Should().NotBe(token);
        harness.Store.Audits.Should().Contain(entry => entry.Action == "ResetPassword");
    }

    private static async Task<AuthResponse> RegisterAndLogin(IdentityHarness harness)
    {
        var registered = await harness.Register().Handle(new RegisterCommand("a@lifefit.ai", Password, "A", "User", null), CancellationToken.None);
        harness.Store.Actor.UserId = registered.Value.User.Id;
        harness.Store.Actor.TenantId = registered.Value.CurrentTenant.Id;
        var login = await harness.Login().Handle(new LoginCommand("a@lifefit.ai", Password), CancellationToken.None);
        return login.Value;
    }
}
