using FluentAssertions;
using LifeFitAI.Application.Ai;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Tests.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Ai;

public sealed class LlmConnectionTests
{
    [Fact]
    public async Task Non_super_admin_cannot_list_connections()
    {
        var handler = Handler(superAdmin: false, out _, out _, out _);
        var result = await handler.Handle(new ListLlmConnectionsQuery(), CancellationToken.None);
        result.Error.Code.Should().Be(IdentityErrors.Forbidden.Code);
    }

    [Fact]
    public async Task Create_stores_a_protected_secret_and_does_not_return_it()
    {
        var handler = Handler(superAdmin: true, out var store, out var audit, out var protector);
        var result = await handler.Handle(new CreateLlmConnectionCommand("Groq", LlmConnection.OpenAiCompatible, "https://api.groq.com/openai/v1", "sk-test-secret", "llama", true, true, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ApiKeyConfigured.Should().BeTrue();
        result.Value.ToString().Should().NotContain("sk-test-secret");
        store.Saved!.ProtectedApiKey.Should().Be("protected:sk-test-secret");
        protector.Plaintext.Should().Be("sk-test-secret");
        audit.Values.Should().NotContain(value => value != null && value.Contains("sk-test-secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Replace_keeps_the_existing_secret_when_a_new_key_is_absent()
    {
        var handler = Handler(superAdmin: true, out var store, out var audit, out _);
        store.Saved = LlmConnection.Create(Guid.NewGuid(), "Groq", "https://api.groq.com/openai/v1", "protected:old", "llama", true, false, 20);
        var result = await handler.Handle(new UpdateLlmConnectionCommand(store.Saved.Id, "Groq", "https://api.groq.com/openai/v1", null, "llama", true, false, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Saved.ProtectedApiKey.Should().Be("protected:old");
        audit.Actions.Should().NotContain("LlmSecretReplaced");
    }

    [Fact]
    public async Task Replace_overwrites_the_protected_secret_without_auditing_the_key()
    {
        var handler = Handler(superAdmin: true, out var store, out var audit, out _);
        store.Saved = LlmConnection.Create(Guid.NewGuid(), "Groq", "https://api.groq.com/openai/v1", "protected:old", "llama", true, false, 20);
        await handler.Handle(new UpdateLlmConnectionCommand(store.Saved.Id, "Groq", "https://api.groq.com/openai/v1", "sk-next", "llama", true, false, 20), CancellationToken.None);

        store.Saved.ProtectedApiKey.Should().Be("protected:sk-next");
        audit.Actions.Should().Contain("LlmSecretReplaced");
        audit.Values.Should().OnlyContain(value => value == null || !value.Contains("sk-next", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Default_connection_cannot_be_deleted()
    {
        var handler = Handler(superAdmin: true, out var store, out _, out _);
        store.Saved = LlmConnection.Create(Guid.NewGuid(), "Groq", "https://api.groq.com/openai/v1", "protected:old", "llama", true, true, 20);
        var result = await handler.Handle(new DeleteLlmConnectionCommand(store.Saved.Id), CancellationToken.None);
        result.Error.Code.Should().Be(LlmErrors.DefaultRequired.Code);
    }

    [Fact]
    public async Task Setting_a_default_clears_the_previous_default()
    {
        var handler = Handler(superAdmin: true, out var store, out var audit, out _);
        store.Saved = LlmConnection.Create(Guid.NewGuid(), "Groq", "https://api.groq.com/openai/v1", "protected:old", "llama", true, false, 20);
        var result = await handler.Handle(new SetDefaultLlmConnectionCommand(store.Saved.Id), CancellationToken.None);

        result.Value.IsDefault.Should().BeTrue();
        store.ClearedDefault.Should().Be(store.Saved.Id);
        audit.Actions.Should().Contain("LlmDefaultChanged");
    }

    [Fact]
    public async Task Connection_test_reports_an_unreadable_saved_key()
    {
        var store = new MemoryStore();
        store.Saved = LlmConnection.Create(Guid.NewGuid(), "Groq", "https://api.groq.com/openai/v1", "ciphertext", "llama", true, true, 20);
        var probe = new FakeProbe();
        var model = new LlmModelHandler(Actor(true), Roles(true), Permissions(true), store, new PrefixProtector(), probe, new FakeAudit(), UnitOfWork(), Clock(), Substitute.For<IClientContext>());

        var result = await model.Handle(new TestLlmConnectionCommand(store.Saved.Id), CancellationToken.None);

        result.Value.Succeeded.Should().BeFalse();
        result.Value.LatencyMs.Should().BeNull();
        result.Value.Message.Should().Be(LlmErrors.SecretUnreadable.Description);
        probe.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Connection_test_does_not_return_the_secret()
    {
        var store = StoreWithSecret();
        var model = new LlmModelHandler(Actor(true), Roles(true), Permissions(true), store, new PrefixProtector(), new FakeProbe(), new FakeAudit(), UnitOfWork(), Clock(), Substitute.For<IClientContext>());
        var result = await model.Handle(new TestLlmConnectionCommand(store.Saved!.Id), CancellationToken.None);
        result.Value.Succeeded.Should().BeTrue();
        result.Value.ToString().Should().NotContain("sk-test-secret");
    }

    private static LlmConnectionHandler Handler(bool superAdmin, out MemoryStore store, out FakeAudit audit, out PrefixProtector protector)
    {
        store = new MemoryStore();
        audit = new FakeAudit();
        protector = new PrefixProtector();
        return new LlmConnectionHandler(Actor(superAdmin), Roles(superAdmin), Permissions(superAdmin), store, protector, audit, UnitOfWork(), Clock(), Substitute.For<IClientContext>());
    }

    private static MemoryStore StoreWithSecret()
    {
        var store = new MemoryStore();
        store.Saved = LlmConnection.Create(Guid.NewGuid(), "Groq", "https://api.groq.com/openai/v1", "protected:sk-test-secret", "llama", true, true, 20);
        return store;
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

    private sealed class MemoryStore : ILlmConnectionStore
    {
        public LlmConnection? Saved { get; set; }

        public Guid? ClearedDefault { get; private set; }

        public Task AddAsync(LlmConnection connection, CancellationToken cancellationToken)
        {
            Saved = connection;
            return Task.CompletedTask;
        }

        public Task<LlmConnection?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Saved?.Id == id ? Saved : null);

        public Task<LlmConnection?> GetDefaultActiveAsync(CancellationToken cancellationToken) => Task.FromResult(Saved is { IsDefault: true, IsActive: true } ? Saved : null);

        public Task<IReadOnlyList<LlmConnection>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LlmConnection>>(Saved is null ? [] : [Saved]);

        public Task<IReadOnlyDictionary<Guid, int>> ModelCountsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task ClearOtherDefaultsAsync(Guid keepId, CancellationToken cancellationToken)
        {
            ClearedDefault = keepId;
            return Task.CompletedTask;
        }

        public Task AddModelAsync(LlmModel model, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<LlmModel?> GetModelAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<LlmModel?>(null);

        public Task<IReadOnlyList<LlmModel>> ListModelsAsync(Guid connectionId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LlmModel>>([]);

        public Task ClearOtherModelDefaultsAsync(Guid connectionId, Guid keepId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> ModelIdentifierExistsAsync(Guid connectionId, string modelIdentifier, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class PrefixProtector : ILlmSecretProtector
    {
        public string? Plaintext { get; private set; }

        public string Protect(string plaintext)
        {
            Plaintext = plaintext;
            return "protected:" + plaintext;
        }

        public string? Unprotect(string protectedValue) => protectedValue.StartsWith("protected:", StringComparison.Ordinal) ? protectedValue["protected:".Length..] : null;
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

    private sealed class FakeProbe : ILlmProbe
    {
        public int Calls { get; private set; }

        public Task<LlmProbeResult> ProbeAsync(string baseUrl, string apiKey, int timeoutSeconds, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new LlmProbeResult(apiKey == "sk-test-secret", 12, ["llama"]));
        }
    }
}
