using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Identity;

public sealed class Role : BaseEntity
{
    private Role()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public static Role Create(Guid id, string name, string? description)
    {
        return new Role
        {
            Id = id,
            Name = name,
            Description = description,
        };
    }
}

public sealed class Permission : BaseEntity
{
    private Permission()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public static Permission Create(Guid id, string code, string name, string? description)
    {
        return new Permission
        {
            Id = id,
            Code = code,
            Name = name,
            Description = description,
        };
    }
}

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        return new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
        };
    }
}

public sealed class UserRole : BaseEntity
{
    private UserRole()
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public Guid TenantId { get; private set; }

    public static UserRole Create(Guid id, Guid userId, Guid roleId, Guid tenantId)
    {
        return new UserRole
        {
            Id = id,
            UserId = userId,
            RoleId = roleId,
            TenantId = tenantId,
        };
    }

    public void Remove(DateTime utcNow) => SoftDelete(utcNow);
}
