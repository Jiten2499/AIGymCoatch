using LifeFitAI.SharedKernel.Abstractions;

namespace LifeFitAI.SharedKernel.Primitives;

public abstract class BaseEntity : IAuditable, ISoftDelete
{
    public Guid Id { get; protected set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public bool IsDeleted => DeletedAt.HasValue;

    public void SoftDelete(DateTime utcNow)
    {
        if (DeletedAt is not null)
        {
            return;
        }

        DeletedAt = utcNow;
    }

    internal void SetAuditCreated(DateTime utcNow, Guid? userId)
    {
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
        CreatedBy = userId;
        UpdatedBy = userId;
    }

    internal void SetAuditUpdated(DateTime utcNow, Guid? userId)
    {
        UpdatedAt = utcNow;
        UpdatedBy = userId;
    }
}
