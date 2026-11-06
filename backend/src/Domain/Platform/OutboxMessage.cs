namespace LifeFitAI.Domain.Platform;

public enum OutboxMessageStatus
{
    Pending = 0,
    Dispatched = 1,
    Cancelled = 2,
}

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string MessageType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTime AvailableAtUtc { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public string? CorrelationId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static string PublishKey(Guid communicationId) => $"Communication.Publish:{communicationId:D}";

    public static OutboxMessage Create(Guid id, Guid tenantId, string messageType, string payload, DateTime availableAtUtc, string? correlationId, string idempotencyKey, DateTime createdAtUtc) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            MessageType = messageType,
            Payload = payload,
            AvailableAtUtc = availableAtUtc,
            Status = OutboxMessageStatus.Pending,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = createdAtUtc,
        };

    public void Reschedule(string payload, DateTime availableAtUtc, string? correlationId)
    {
        Payload = payload;
        AvailableAtUtc = availableAtUtc;
        CorrelationId = correlationId;
        Status = OutboxMessageStatus.Pending;
        ProcessedAtUtc = null;
        LastError = null;
    }

    public void MarkDispatched(string payload, DateTime utcNow)
    {
        Payload = payload;
        Status = OutboxMessageStatus.Dispatched;
        ProcessedAtUtc = utcNow;
        LastError = null;
    }

    public void Cancel(DateTime utcNow)
    {
        Status = OutboxMessageStatus.Cancelled;
        ProcessedAtUtc = utcNow;
    }

    public void Fail(string error)
    {
        AttemptCount++;
        LastError = error.Length <= 500 ? error : error[..500];
    }

    public void Defer(string error, DateTime availableAtUtc)
    {
        AttemptCount++;
        LastError = error.Length <= 500 ? error : error[..500];
        AvailableAtUtc = availableAtUtc;
        Status = OutboxMessageStatus.Pending;
        ProcessedAtUtc = null;
    }
}
