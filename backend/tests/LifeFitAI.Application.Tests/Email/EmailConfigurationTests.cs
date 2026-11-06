using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Email;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Email;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Email;

public sealed class EmailConfigurationTests
{
    [Fact]
    public async Task Non_super_admin_cannot_read_configuration()
    {
        var handler = Handler(superAdmin: false, out _, out _, out _);
        var result = await handler.Handle(new GetEmailConfigurationQuery(), CancellationToken.None);
        result.Error.Code.Should().Be(IdentityErrors.Forbidden.Code);
    }

    [Fact]
    public async Task Save_protects_password_and_never_returns_it()
    {
        var handler = Handler(superAdmin: true, out var store, out var audit, out var protector);
        var result = await handler.Handle(new SaveEmailConfigurationCommand(
            EmailProviders.SmtpHostinger,
            "smtp.hostinger.com",
            465,
            EmailEncryption.SslTls,
            true,
            "notifications@mindgridtech.com",
            "s3cret-pass",
            "LifeFit AI",
            "notifications@mindgridtech.com",
            "support@mindgridtech.com",
            30,
            2,
            5,
            60,
            true,
            true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PasswordConfigured.Should().BeTrue();
        result.Value.ToString().Should().NotContain("s3cret-pass");
        store.Saved!.ProtectedPassword.Should().Be("protected:s3cret-pass");
        protector.Plaintext.Should().Be("s3cret-pass");
        audit.Values.Should().OnlyContain(value => value == null || !value.Contains("s3cret-pass", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Save_keeps_existing_password_when_blank()
    {
        var handler = Handler(superAdmin: true, out var store, out var audit, out _);
        store.Saved = EmailProviderConfiguration.CreateHostingerDefaults(Guid.NewGuid());
        store.Saved.ReplacePassword("protected:old");
        var result = await handler.Handle(new SaveEmailConfigurationCommand(
            EmailProviders.SmtpHostinger,
            "smtp.hostinger.com",
            465,
            EmailEncryption.SslTls,
            true,
            "notifications@mindgridtech.com",
            null,
            "LifeFit AI",
            "notifications@mindgridtech.com",
            null,
            30,
            2,
            5,
            60,
            true,
            true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Saved.ProtectedPassword.Should().Be("protected:old");
        audit.Actions.Should().NotContain("EmailSecretReplaced");
    }

    [Fact]
    public async Task Unsupported_provider_is_rejected()
    {
        var handler = Handler(superAdmin: true, out _, out _, out _);
        var result = await handler.Handle(new SaveEmailConfigurationCommand(
            EmailProviders.SendGrid,
            "smtp.hostinger.com",
            465,
            EmailEncryption.SslTls,
            true,
            "a@b.com",
            "x",
            "LifeFit AI",
            "a@b.com",
            null,
            30,
            2,
            5,
            60,
            true,
            true), CancellationToken.None);

        result.Error.Code.Should().Be(EmailErrors.ProviderUnsupported.Code);
    }

    [Fact]
    public async Task Connection_test_does_not_return_secret()
    {
        var store = new MemoryStore
        {
            Saved = EmailProviderConfiguration.CreateHostingerDefaults(Guid.NewGuid()),
        };
        store.Saved.ReplacePassword("protected:s3cret-pass");
        store.Saved.UpdateSmtp("smtp.hostinger.com", 465, EmailEncryption.SslTls, true, "user", "LifeFit AI", "a@b.com", null, 30, 2, 5, 60, true);
        var provider = new FakeProvider();
        var handler = new EmailConfigurationHandler(
            Actor(true),
            Roles(true),
            Permissions(true),
            store,
            new PassthroughCache(store),
            new PrefixProtector(),
            new FixedResolver(provider),
            Substitute.For<IEmailService>(),
            Users(),
            new FakeAudit(),
            UnitOfWork(),
            Clock(),
            Substitute.For<IClientContext>());

        var result = await handler.Handle(new TestEmailConnectionCommand(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Succeeded.Should().BeTrue();
        result.Value.ToString().Should().NotContain("s3cret-pass");
        provider.Calls.Should().Be(1);
    }

    private static EmailConfigurationHandler Handler(bool superAdmin, out MemoryStore store, out FakeAudit audit, out PrefixProtector protector)
    {
        store = new MemoryStore();
        audit = new FakeAudit();
        protector = new PrefixProtector();
        return new EmailConfigurationHandler(
            Actor(superAdmin),
            Roles(superAdmin),
            Permissions(superAdmin),
            store,
            new PassthroughCache(store),
            protector,
            new FixedResolver(new FakeProvider()),
            Substitute.For<IEmailService>(),
            Users(),
            audit,
            UnitOfWork(),
            Clock(),
            Substitute.For<IClientContext>());
    }

    private static FakeActor Actor(bool superAdmin) => new() { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid() };

    private static IRoleRepository Roles(bool superAdmin)
    {
        var roles = Substitute.For<IRoleRepository>();
        roles.GetRoleNamesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(superAdmin ? new[] { RoleNames.SuperAdmin } : new[] { RoleNames.PersonalOwner });
        return roles;
    }

    private static IPermissionService Permissions(bool superAdmin)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), PermissionCodes.PlatformAdmin, Arg.Any<CancellationToken>()).Returns(superAdmin);
        return permissions;
    }

    private static IUserAccountRepository Users()
    {
        var users = Substitute.For<IUserAccountRepository>();
        users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        return users;
    }

    private static IUnitOfWork UnitOfWork()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return unitOfWork;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        return clock;
    }

    private sealed class FakeActor : ICurrentActor
    {
        public Guid? UserId { get; init; }

        public Guid? TenantId { get; init; }
    }

    private sealed class MemoryStore : IEmailConfigStore
    {
        public EmailProviderConfiguration? Saved { get; set; }

        public Task AddAsync(EmailProviderConfiguration configuration, CancellationToken cancellationToken)
        {
            Saved = configuration;
            return Task.CompletedTask;
        }

        public Task ClearOtherActiveAsync(Guid keepId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<EmailProviderConfiguration?> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult(Saved);

        public Task<EmailProviderConfiguration?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Saved?.Id == id ? Saved : null);
    }

    private sealed class PassthroughCache(MemoryStore store) : IEmailConfigCache
    {
        public void Invalidate()
        {
        }

        public Task<EmailProviderConfiguration?> GetActiveAsync(CancellationToken cancellationToken) => store.GetActiveAsync(cancellationToken);
    }

    private sealed class PrefixProtector : IEmailSecretProtector
    {
        public string? Plaintext { get; private set; }

        public string Protect(string plaintext)
        {
            Plaintext = plaintext;
            return "protected:" + plaintext;
        }

        public string? Unprotect(string protectedValue) =>
            protectedValue.StartsWith("protected:", StringComparison.Ordinal) ? protectedValue["protected:".Length..] : null;
    }

    private sealed class FakeAudit : IAuditLogRepository
    {
        public List<string> Actions { get; } = [];

        public List<string?> Values { get; } = [];

        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken)
        {
            Actions.Add(entry.Action);
            Values.Add(entry.NewValues);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProvider : IEmailProvider
    {
        public int Calls { get; private set; }

        public string ProviderKey => EmailProviders.SmtpHostinger;

        public Task<EmailSendResult> SendAsync(EmailProviderSettings settings, EmailMessageRequest message, CancellationToken cancellationToken) =>
            Task.FromResult(new EmailSendResult(true, "msg", null, false));

        public Task<EmailTestResponse> TestConnectionAsync(EmailProviderSettings settings, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new EmailTestResponse(settings.Password == "s3cret-pass", "Healthy", "ok", DateTime.UtcNow));
        }
    }

    private sealed class FixedResolver(IEmailProvider provider) : IEmailProviderResolver
    {
        public IEmailProvider Resolve(string providerKey) => provider;

        public IReadOnlyList<EmailProviderOption> ListOptions() => [new(EmailProviders.SmtpHostinger, "SMTP (Hostinger)", true)];
    }
}
