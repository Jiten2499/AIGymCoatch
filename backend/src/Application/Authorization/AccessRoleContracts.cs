namespace LifeFitAI.Application.Authorization;

public sealed record AccessRoleSummary(
    Guid Id,
    string Name,
    string Label,
    string? Description,
    bool IsSystem,
    bool IsProtected,
    bool IsActive,
    int AssignedUsers,
    string PermissionSummary,
    IReadOnlyList<string> PermissionCodes,
    IReadOnlyList<AccessScopeInput> Scopes);

public sealed record AccessRoleList(bool CanManage, IReadOnlyList<AccessRoleSummary> Roles);

public sealed record AccessCatalogModule(string Key, string Name, bool Scoped, IReadOnlyList<AccessAction> Actions);

public sealed record AccessAssignment(
    Guid UserId,
    string DisplayName,
    string Email,
    Guid? RoleId,
    string RoleName,
    string RoleLabel,
    bool HasPhoto,
    string AccountStatus,
    string SeatStatus,
    string MembershipStatus);

public sealed record AccessOverviewRow(
    Guid UserId,
    string DisplayName,
    string RoleLabel,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Excessive,
    IReadOnlyList<string> RestrictedModules,
    string MemberScope);

public sealed record EffectiveAccess(
    Guid UserId,
    string DisplayName,
    string RoleLabel,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<AccessScopeInput> Scopes);
