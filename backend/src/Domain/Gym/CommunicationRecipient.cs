using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum CommunicationRecipientKind
{
    Member = 0,
    Trainer = 1,
    Staff = 2,
    BusinessUser = 3,
}

public enum CommunicationTargetKind
{
    Trainer = 1,
    Staff = 2,
}

public sealed class CommunicationAudienceTarget : BaseEntity
{
    private CommunicationAudienceTarget()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid CommunicationId { get; private set; }

    public CommunicationTargetKind TargetKind { get; private set; }

    public Guid EntityId { get; private set; }

    public static CommunicationAudienceTarget Create(Guid id, Guid tenantId, Guid communicationId, CommunicationTargetKind kind, Guid entityId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            CommunicationId = communicationId,
            TargetKind = kind,
            EntityId = entityId,
        };
}

public sealed class CommunicationRecipient : BaseEntity
{
    private CommunicationRecipient()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid CommunicationId { get; private set; }

    public Guid UserId { get; private set; }

    public CommunicationRecipientKind RecipientKind { get; private set; }

    public Guid? RecipientEntityId { get; private set; }

    public DateTime DeliveredAt { get; private set; }

    public DateTime? ReadAt { get; private set; }

    public static CommunicationRecipient Create(
        Guid id,
        Guid tenantId,
        Guid communicationId,
        Guid userId,
        CommunicationRecipientKind kind,
        Guid? entityId,
        DateTime deliveredAt) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            CommunicationId = communicationId,
            UserId = userId,
            RecipientKind = kind,
            RecipientEntityId = entityId,
            DeliveredAt = deliveredAt,
        };

    public void MarkRead(DateTime utcNow)
    {
        ReadAt ??= utcNow;
    }
}
