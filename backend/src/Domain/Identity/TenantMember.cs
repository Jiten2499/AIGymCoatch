using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Identity;

public sealed class TenantMember : BaseEntity
{
    private TenantMember()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public static TenantMember Create(Guid id, Guid tenantId, Guid userId, DateTime joinedAt)
    {
        return new TenantMember
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            Status = MembershipStatus.Active,
            JoinedAt = joinedAt,
        };
    }

    public void Remove(DateTime utcNow)
    {
        Status = MembershipStatus.Removed;
        SoftDelete(utcNow);
    }
}
