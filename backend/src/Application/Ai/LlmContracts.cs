using LifeFitAI.Domain.Fitness;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Ai;

public static class LlmErrors
{
    public static readonly Error NotFound = new("llm.not_found", "The LLM connection was not found.");
    public static readonly Error DefaultRequired = new("llm.default_required", "Choose another default connection before disabling or deleting this one.");
    public static readonly Error SecretRequired = new("llm.secret_required", "An API key is required.");
    public static readonly Error UnsafeUrl = new("llm.invalid", "The endpoint must be an https address, or http on localhost.");
    public static readonly Error SecretUnreadable = new("llm.secret_unreadable", "The saved API key cannot be read. Replace the API key, save, and test again.");
}

public sealed record LlmConnectionResponse(
    Guid Id,
    string Name,
    string Provider,
    string BaseUrl,
    bool ApiKeyConfigured,
    string? DefaultModel,
    bool IsActive,
    bool IsDefault,
    int TimeoutSeconds,
    DateTime? LastTestedAt,
    string HealthStatus,
    int? LastLatencyMs,
    int ModelCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record LlmModelResponse(Guid Id, Guid ConnectionId, string ModelIdentifier, string DisplayName, bool IsActive, bool IsDefault, IReadOnlyList<string> Capabilities);

public sealed record LlmTestResponse(bool Succeeded, string HealthStatus, int? LatencyMs, IReadOnlyList<string> Models, string? Message);

public sealed record LlmCallSettings(string Provider, string BaseUrl, string ApiKey, string? Model, int TimeoutSeconds);

public interface ILlmSecretProtector
{
    string Protect(string plaintext);

    string? Unprotect(string protectedValue);
}

public interface ILlmConnectionStore
{
    Task AddAsync(LlmConnection connection, CancellationToken cancellationToken);

    Task<LlmConnection?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<LlmConnection?> GetDefaultActiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LlmConnection>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> ModelCountsAsync(CancellationToken cancellationToken);

    Task ClearOtherDefaultsAsync(Guid keepId, CancellationToken cancellationToken);

    Task AddModelAsync(LlmModel model, CancellationToken cancellationToken);

    Task<LlmModel?> GetModelAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<LlmModel>> ListModelsAsync(Guid connectionId, CancellationToken cancellationToken);

    Task ClearOtherModelDefaultsAsync(Guid connectionId, Guid keepId, CancellationToken cancellationToken);

    Task<bool> ModelIdentifierExistsAsync(Guid connectionId, string modelIdentifier, CancellationToken cancellationToken);
}

public interface ILlmProbe
{
    Task<LlmProbeResult> ProbeAsync(string baseUrl, string apiKey, int timeoutSeconds, CancellationToken cancellationToken);
}

public sealed record LlmProbeResult(bool Succeeded, int LatencyMs, IReadOnlyList<string> ModelIds);

public interface ILlmRuntime
{
    Task<LlmCallSettings?> ResolveAsync(CancellationToken cancellationToken);
}
