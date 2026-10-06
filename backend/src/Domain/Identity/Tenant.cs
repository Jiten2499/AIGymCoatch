using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Identity;

public sealed class Tenant : BaseEntity
{
    private Tenant()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public TenantType TenantType { get; private set; }

    public TenantStatus Status { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public static Tenant Create(Guid id, string name, TenantType tenantType)
    {
        return new Tenant
        {
            Id = id,
            Name = name,
            TenantType = tenantType,
            Status = TenantStatus.Active,
        };
    }

    public void AssignOwner(Guid userId) => OwnerUserId = userId;

    public void Rename(string name) => Name = name.Trim();

    public bool ChangeType(TenantType tenantType)
    {
        if (TenantType == TenantType.Platform || tenantType == TenantType.Platform)
        {
            return false;
        }

        TenantType = tenantType;
        return true;
    }

    public void SetStatus(TenantStatus status) => Status = status;

    public void Suspend() => Status = TenantStatus.Suspended;
}
