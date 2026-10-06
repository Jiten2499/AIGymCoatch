namespace LifeFitAI.Domain.Identity;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string Operation { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public int StatusCode { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static IdempotencyRecord Create(Guid id, Guid tenantId, Guid userId, string operation, string key, int statusCode, DateTime createdAt) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            Operation = operation.Length <= 100 ? operation : operation[..100],
            IdempotencyKey = key.Length <= 100 ? key : key[..100],
            StatusCode = statusCode,
            CreatedAt = createdAt,
        };
}
