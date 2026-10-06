using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Fitness;

public sealed class LlmConnection : BaseEntity
{
    public const string OpenAiCompatible = "OpenAiCompatible";

    private LlmConnection()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Provider { get; private set; } = OpenAiCompatible;

    public string BaseUrl { get; private set; } = string.Empty;

    public string ProtectedApiKey { get; private set; } = string.Empty;

    public string? DefaultModel { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsDefault { get; private set; }

    public int TimeoutSeconds { get; private set; }

    public DateTime? LastTestedAt { get; private set; }

    public string HealthStatus { get; private set; } = "NotTested";

    public int? LastLatencyMs { get; private set; }

    public static LlmConnection Create(Guid id, string name, string baseUrl, string protectedApiKey, string? defaultModel, bool isActive, bool isDefault, int timeoutSeconds)
    {
        return new LlmConnection
        {
            Id = id,
            Name = name.Trim(),
            Provider = OpenAiCompatible,
            BaseUrl = baseUrl.Trim().TrimEnd('/'),
            ProtectedApiKey = protectedApiKey,
            DefaultModel = string.IsNullOrWhiteSpace(defaultModel) ? null : defaultModel.Trim(),
            IsActive = isActive,
            IsDefault = isDefault,
            TimeoutSeconds = timeoutSeconds,
            HealthStatus = "NotTested",
        };
    }

    public void Update(string name, string baseUrl, string? defaultModel, bool isActive, bool isDefault, int timeoutSeconds)
    {
        Name = name.Trim();
        BaseUrl = baseUrl.Trim().TrimEnd('/');
        DefaultModel = string.IsNullOrWhiteSpace(defaultModel) ? null : defaultModel.Trim();
        IsActive = isActive;
        IsDefault = isDefault;
        TimeoutSeconds = timeoutSeconds;
    }

    public void ReplaceSecret(string protectedApiKey) => ProtectedApiKey = protectedApiKey;

    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    public void RecordTest(bool healthy, int latencyMs, DateTime utcNow)
    {
        LastTestedAt = utcNow;
        LastLatencyMs = latencyMs;
        HealthStatus = healthy ? "Healthy" : "Failed";
    }
}

public sealed class LlmModel : BaseEntity
{
    private LlmModel()
    {
    }

    public Guid ConnectionId { get; private set; }

    public string ModelIdentifier { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool IsDefault { get; private set; }

    public static LlmModel Create(Guid id, Guid connectionId, string modelIdentifier, string displayName, bool isActive, bool isDefault)
    {
        return new LlmModel
        {
            Id = id,
            ConnectionId = connectionId,
            ModelIdentifier = modelIdentifier.Trim(),
            DisplayName = displayName.Trim(),
            IsActive = isActive,
            IsDefault = isDefault,
        };
    }

    public void Update(string displayName, bool isActive, bool isDefault)
    {
        DisplayName = displayName.Trim();
        IsActive = isActive;
        IsDefault = isDefault;
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;
}
