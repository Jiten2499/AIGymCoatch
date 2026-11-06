using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Identity;

public sealed class Role : BaseEntity
{
    private Role()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid? TenantId { get; private set; }

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsProtectedOwner => IsSystem && TenantId is null && Name == RoleNames.GymOwner;

    public static Role Create(Guid id, string name, string? description)
    {
        return new Role
        {
            Id = id,
            Name = name,
            Description = description,
            IsSystem = true,
            IsActive = true,
        };
    }

    public static Role CreateCustom(Guid id, Guid tenantId, string name, string? description)
    {
        return new Role
        {
            Id = id,
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description,
            IsSystem = false,
            IsActive = true,
        };
    }

    public bool TryRename(string name, string? description)
    {
        if (IsSystem || IsProtectedOwner)
        {
            return false;
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return true;
    }

    public bool TrySetActive(bool active)
    {
        if (IsProtectedOwner)
        {
            return false;
        }

        IsActive = active;
        return true;
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

public sealed class RoleResourceScope
{
    private RoleResourceScope()
    {
    }

    public Guid RoleId { get; private set; }

    public string Resource { get; private set; } = string.Empty;

    public string Scope { get; private set; } = string.Empty;

    public static RoleResourceScope Create(Guid roleId, string resource, string scope)
    {
        return new RoleResourceScope
        {
            RoleId = roleId,
            Resource = resource,
            Scope = scope,
        };
    }
}

public static class AccessScopes
{
    public const string All = "All";
    public const string Branch = "Branch";
    public const string Assigned = "Assigned";
    public const string Own = "Own";

    public static bool IsKnown(string? scope) => scope is All or Branch or Assigned or Own;

    public static string MostPermissive(IEnumerable<string> scopes)
    {
        var values = scopes.ToArray();
        if (values.Contains(All, StringComparer.Ordinal))
        {
            return All;
        }

        if (values.Contains(Branch, StringComparer.Ordinal))
        {
            return Branch;
        }

        return values.Contains(Assigned, StringComparer.Ordinal) ? Assigned : Own;
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

public sealed class UserPermissionOverride : BaseEntity
{
    private UserPermissionOverride()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid PermissionId { get; private set; }

    public bool Granted { get; private set; }

    public static UserPermissionOverride Create(Guid id, Guid tenantId, Guid userId, Guid permissionId, bool granted)
    {
        return new UserPermissionOverride
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            PermissionId = permissionId,
            Granted = granted,
        };
    }
}

public sealed class UserResourceScope : BaseEntity
{
    private UserResourceScope()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string Resource { get; private set; } = string.Empty;

    public string Scope { get; private set; } = string.Empty;

    public static UserResourceScope Create(Guid id, Guid tenantId, Guid userId, string resource, string scope)
    {
        return new UserResourceScope
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            Resource = resource,
            Scope = scope,
        };
    }
}
