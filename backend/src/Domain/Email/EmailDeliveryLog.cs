using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Email;

public static class EmailDeliveryStatuses
{
    public const string Queued = "Queued";

    public const string Processing = "Processing";

    public const string Accepted = "Accepted";

    public const string Delivered = "Delivered";

    public const string Retrying = "Retrying";

    public const string Failed = "Failed";

    public const string Bounced = "Bounced";

    public const string Suppressed = "Suppressed";
}

public static class EmailClassifications
{
    public const string Transactional = "Transactional";

    public const string Test = "Test";
}

public sealed class EmailDeliveryLog : BaseEntity
{
    private EmailDeliveryLog()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid? RelatedTenantId { get; private set; }

    public string Recipient { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string TemplateKey { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public string Status { get; private set; } = EmailDeliveryStatuses.Queued;

    public string Classification { get; private set; } = EmailClassifications.Transactional;

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? FailureReason { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public Guid? OutboxMessageId { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTime QueuedAt { get; private set; }

    public DateTime? SentAt { get; private set; }

    public static EmailDeliveryLog Create(
        Guid id,
        Guid tenantId,
        Guid? relatedTenantId,
        string recipient,
        string subject,
        string templateKey,
        string provider,
        string classification,
        int maxAttempts,
        string idempotencyKey,
        string? correlationId,
        DateTime utcNow) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            RelatedTenantId = relatedTenantId,
            Recipient = recipient.Trim().ToLowerInvariant(),
            Subject = subject.Trim(),
            TemplateKey = templateKey.Trim(),
            Provider = provider,
            Status = EmailDeliveryStatuses.Queued,
            Classification = classification,
            AttemptCount = 0,
            MaxAttempts = maxAttempts,
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId,
            QueuedAt = utcNow,
        };

    public void BindOutbox(Guid outboxMessageId) => OutboxMessageId = outboxMessageId;

    public void MarkProcessing()
    {
        Status = EmailDeliveryStatuses.Processing;
        AttemptCount++;
    }

    public void MarkAccepted(string? providerMessageId, DateTime utcNow)
    {
        Status = EmailDeliveryStatuses.Accepted;
        ProviderMessageId = Truncate(providerMessageId, 200);
        SentAt = utcNow;
        FailureReason = null;
    }

    public void MarkRetrying(string reason)
    {
        Status = EmailDeliveryStatuses.Retrying;
        FailureReason = Truncate(reason, 500);
    }

    public void MarkFailed(string reason)
    {
        Status = EmailDeliveryStatuses.Failed;
        FailureReason = Truncate(reason, 500);
    }

    public void ResetForRetry()
    {
        if (Status is not (EmailDeliveryStatuses.Failed or EmailDeliveryStatuses.Retrying))
        {
            return;
        }

        Status = EmailDeliveryStatuses.Queued;
        FailureReason = null;
    }

    public bool CanRetry =>
        Status is EmailDeliveryStatuses.Failed or EmailDeliveryStatuses.Retrying
        && AttemptCount < MaxAttempts;

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
