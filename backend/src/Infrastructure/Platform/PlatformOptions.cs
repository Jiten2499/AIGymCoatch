namespace LifeFitAI.Infrastructure.Platform;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string[] AllowedOrigins { get; set; } = [];

    public bool MaintenanceMode { get; set; }

    public int MaxRequestBytes { get; set; } = 1_048_576;

    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int GlobalPermitLimit { get; set; } = 300;

    public int GlobalWindowSeconds { get; set; } = 60;

    public int AuthPermitLimit { get; set; } = 10;

    public int AuthWindowSeconds { get; set; } = 60;

    public int AiPermitLimit { get; set; } = 20;

    public int AiWindowSeconds { get; set; } = 60;

    public int CheckInPermitLimit { get; set; } = 30;

    public int CheckInWindowSeconds { get; set; } = 60;
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; }

    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";

    public string RootPath { get; set; } = "storage";
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;
}

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool Enabled { get; set; } = true;

    public string? OtlpEndpoint { get; set; }
}
