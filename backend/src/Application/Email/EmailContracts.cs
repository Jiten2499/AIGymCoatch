using LifeFitAI.Domain.Email;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Email;

public static class EmailErrors
{
    public static readonly Error NotFound = new("email.config_not_found", "Email provider configuration was not found.");

    public static readonly Error ProviderUnsupported = new("email.provider_unsupported", "That email provider is not available yet.");

    public static readonly Error SecretRequired = new("email.secret_required", "An SMTP password is required when authentication is enabled.");

    public static readonly Error SecretUnreadable = new("email.secret_unreadable", "The saved SMTP password cannot be read. Replace the password, save, and test again.");

    public static readonly Error Disabled = new("email.disabled", "The email provider is disabled.");

    public static readonly Error NotConfigured = new("email.not_configured", "Configure and enable an email provider before sending.");

    public static readonly Error DeliveryNotFound = new("email.delivery_not_found", "The email delivery log was not found.");

    public static readonly Error RetryNotAllowed = new("email.retry_not_allowed", "This message cannot be retried.");

    public static readonly Error RateWarning = new("email.rate_warning", "Hostinger Business Email typically supports up to about 100 messages per hour. Prefer a lower per-minute limit.");
}

public static class EmailTemplateKeys
{
    public const string PasswordReset = "Account.PasswordReset";

    public const string InvitationStaff = "Account.Invitation.Staff";

    public const string InvitationTrainer = "Account.Invitation.Trainer";

    public const string InvitationMember = "Account.Invitation.Member";

    public const string WelcomeStaff = "Account.Welcome.Staff";

    public const string WelcomeTrainer = "Account.Welcome.Trainer";

    public const string WelcomeMember = "Account.Welcome.Member";

    public const string WelcomeOwner = "Account.Welcome.Owner";

    public const string TestEmail = "System.TestEmail";
}

public sealed record EmailConfigurationResponse(
    Guid? Id,
    string Provider,
    string DisplayName,
    bool IsEnabled,
    bool IsActive,
    string SmtpHost,
    int SmtpPort,
    string Encryption,
    bool AuthenticationEnabled,
    string Username,
    bool PasswordConfigured,
    string SenderName,
    string SenderEmail,
    string? ReplyToEmail,
    int ConnectionTimeoutSeconds,
    int MaxEmailsPerMinute,
    int MaxDeliveryAttempts,
    int RetryBaseDelaySeconds,
    int ConfigVersion,
    DateTime? LastTestedAt,
    string? LastTestStatus,
    string? LastTestMessage,
    DateTime? LastEmailSentAt,
    string? LastEmailStatus,
    string HealthStatus,
    DateTime? UpdatedAt,
    string? UpdatedByName,
    IReadOnlyList<EmailProviderOption> AvailableProviders,
    string? RateLimitWarning);

public sealed record EmailProviderOption(string Provider, string DisplayName, bool Implemented);

public sealed record EmailTestResponse(bool Succeeded, string HealthStatus, string Message, DateTime TestedAt);

public sealed record EmailDeliveryLogResponse(
    Guid Id,
    Guid TenantId,
    Guid? RelatedTenantId,
    string Recipient,
    string Subject,
    string TemplateKey,
    string Provider,
    string Status,
    string Classification,
    int AttemptCount,
    int MaxAttempts,
    string? ProviderMessageId,
    string? FailureReason,
    DateTime QueuedAt,
    DateTime? SentAt,
    bool CanRetry);

public sealed record EmailDeliveryPage(IReadOnlyList<EmailDeliveryLogResponse> Items, int Page, int PageSize, int TotalCount);

public sealed record EmailMessageRequest(
    string Recipient,
    string Subject,
    string HtmlBody,
    string TextBody,
    string TemplateKey,
    Guid TenantId,
    Guid? RelatedTenantId,
    string Classification,
    string IdempotencyKey,
    string? CorrelationId);

public sealed record EmailSendResult(bool Succeeded, string? ProviderMessageId, string? Error, bool Transient);

public sealed record EmailProviderSettings(
    string Provider,
    string SmtpHost,
    int SmtpPort,
    string Encryption,
    bool AuthenticationEnabled,
    string Username,
    string? Password,
    string SenderName,
    string SenderEmail,
    string? ReplyToEmail,
    int ConnectionTimeoutSeconds);

public interface IEmailSecretProtector
{
    string Protect(string plaintext);

    string? Unprotect(string protectedValue);
}

public interface IEmailConfigStore
{
    Task<EmailProviderConfiguration?> GetActiveAsync(CancellationToken cancellationToken);

    Task<EmailProviderConfiguration?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(EmailProviderConfiguration configuration, CancellationToken cancellationToken);

    Task ClearOtherActiveAsync(Guid keepId, CancellationToken cancellationToken);
}

public interface IEmailDeliveryStore
{
    Task AddAsync(EmailDeliveryLog log, CancellationToken cancellationToken);

    Task<EmailDeliveryLog?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<EmailDeliveryLog?> FindByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<(IReadOnlyList<EmailDeliveryLog> Items, int Total)> ListAsync(
        string? search,
        string? status,
        string? templateKey,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public interface IEmailConfigCache
{
    void Invalidate();

    Task<EmailProviderConfiguration?> GetActiveAsync(CancellationToken cancellationToken);
}

public interface IEmailProvider
{
    string ProviderKey { get; }

    Task<EmailSendResult> SendAsync(EmailProviderSettings settings, EmailMessageRequest message, CancellationToken cancellationToken);

    Task<EmailTestResponse> TestConnectionAsync(EmailProviderSettings settings, CancellationToken cancellationToken);
}

public interface IEmailProviderResolver
{
    IEmailProvider Resolve(string provider);

    IReadOnlyList<EmailProviderOption> ListOptions();
}

public interface IEmailTemplateRenderer
{
    (string Subject, string HtmlBody, string TextBody) Render(string templateKey, IReadOnlyDictionary<string, string> values);
}

public interface IEmailService
{
    Task<Result<Guid>> QueueAsync(EmailMessageRequest request, CancellationToken cancellationToken);
}
