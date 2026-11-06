using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Email;

public static class EmailProviders
{
    public const string SmtpHostinger = "SmtpHostinger";

    public const string SmtpGeneric = "SmtpGeneric";

    public const string SendGrid = "SendGrid";

    public const string AmazonSes = "AmazonSes";

    public static bool IsSmtp(string provider) =>
        provider is SmtpHostinger or SmtpGeneric;

    public static bool IsImplemented(string provider) => IsSmtp(provider);
}

public static class EmailEncryption
{
    public const string SslTls = "SslTls";

    public const string StartTls = "StartTls";
}

public sealed class EmailProviderConfiguration : BaseEntity
{
    private EmailProviderConfiguration()
    {
    }

    public string Provider { get; private set; } = EmailProviders.SmtpHostinger;

    public string DisplayName { get; private set; } = "SMTP (Hostinger)";

    public bool IsEnabled { get; private set; }

    public bool IsActive { get; private set; }

    public string SmtpHost { get; private set; } = "smtp.hostinger.com";

    public int SmtpPort { get; private set; } = 465;

    public string Encryption { get; private set; } = EmailEncryption.SslTls;

    public bool AuthenticationEnabled { get; private set; } = true;

    public string Username { get; private set; } = string.Empty;

    public string? ProtectedPassword { get; private set; }

    public string SenderName { get; private set; } = "LifeFit AI";

    public string SenderEmail { get; private set; } = string.Empty;

    public string? ReplyToEmail { get; private set; }

    public int ConnectionTimeoutSeconds { get; private set; } = 30;

    public int MaxEmailsPerMinute { get; private set; } = 30;

    public int MaxDeliveryAttempts { get; private set; } = 5;

    public int RetryBaseDelaySeconds { get; private set; } = 60;

    public int ConfigVersion { get; private set; } = 1;

    public DateTime? LastTestedAt { get; private set; }

    public string? LastTestStatus { get; private set; }

    public string? LastTestMessage { get; private set; }

    public DateTime? LastEmailSentAt { get; private set; }

    public string? LastEmailStatus { get; private set; }

    public string HealthStatus { get; private set; } = "NotTested";

    public static EmailProviderConfiguration CreateHostingerDefaults(Guid id) =>
        new()
        {
            Id = id,
            Provider = EmailProviders.SmtpHostinger,
            DisplayName = "SMTP (Hostinger)",
            IsEnabled = false,
            IsActive = true,
            SmtpHost = "smtp.hostinger.com",
            SmtpPort = 465,
            Encryption = EmailEncryption.SslTls,
            AuthenticationEnabled = true,
            Username = "notifications@mindgridtech.com",
            SenderName = "LifeFit AI",
            SenderEmail = "notifications@mindgridtech.com",
            ReplyToEmail = "support@mindgridtech.com",
            ConnectionTimeoutSeconds = 30,
            MaxEmailsPerMinute = 30,
            MaxDeliveryAttempts = 5,
            RetryBaseDelaySeconds = 60,
            ConfigVersion = 1,
            HealthStatus = "NotTested",
        };

    public void UpdateSmtp(
        string smtpHost,
        int smtpPort,
        string encryption,
        bool authenticationEnabled,
        string username,
        string senderName,
        string senderEmail,
        string? replyToEmail,
        int connectionTimeoutSeconds,
        int maxEmailsPerMinute,
        int maxDeliveryAttempts,
        int retryBaseDelaySeconds,
        bool isEnabled)
    {
        SmtpHost = smtpHost.Trim();
        SmtpPort = smtpPort;
        Encryption = encryption;
        AuthenticationEnabled = authenticationEnabled;
        Username = username.Trim();
        SenderName = senderName.Trim();
        SenderEmail = senderEmail.Trim();
        ReplyToEmail = string.IsNullOrWhiteSpace(replyToEmail) ? null : replyToEmail.Trim();
        ConnectionTimeoutSeconds = connectionTimeoutSeconds;
        MaxEmailsPerMinute = maxEmailsPerMinute;
        MaxDeliveryAttempts = maxDeliveryAttempts;
        RetryBaseDelaySeconds = retryBaseDelaySeconds;
        IsEnabled = isEnabled;
        HealthStatus = isEnabled ? (HealthStatus == "Disabled" ? "NotTested" : HealthStatus) : "Disabled";
        ConfigVersion++;
    }

    public void ReplacePassword(string protectedPassword)
    {
        ProtectedPassword = protectedPassword;
        ConfigVersion++;
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        HealthStatus = enabled ? (HealthStatus == "Disabled" ? "NotTested" : HealthStatus) : "Disabled";
        ConfigVersion++;
    }

    public void Activate()
    {
        IsActive = true;
        ConfigVersion++;
    }

    public void Deactivate() => IsActive = false;

    public void RecordConnectionTest(bool succeeded, string message, DateTime utcNow)
    {
        LastTestedAt = utcNow;
        LastTestStatus = succeeded ? "Success" : "Failed";
        LastTestMessage = Truncate(message, 500);
        HealthStatus = succeeded ? "Healthy" : "Failed";
    }

    public void RecordEmailSent(bool succeeded, DateTime utcNow)
    {
        LastEmailSentAt = utcNow;
        LastEmailStatus = succeeded ? "Success" : "Failed";
        if (succeeded && IsEnabled)
        {
            HealthStatus = "Healthy";
        }
    }

    public bool HasPassword => !string.IsNullOrEmpty(ProtectedPassword);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
