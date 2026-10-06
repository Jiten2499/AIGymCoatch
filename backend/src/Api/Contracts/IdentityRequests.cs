namespace LifeFitAI.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, string? Phone);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record SwitchTenantRequest(Guid TenantId);

public sealed record CreateTenantRequest(string Name, LifeFitAI.Domain.Identity.TenantType TenantType);

public sealed record CreateBusinessTenantRequest(string Name, LifeFitAI.Domain.Identity.TenantType TenantType, string OwnerEmail, string OwnerFirstName, string OwnerLastName);

public sealed record UpdateBusinessTenantRequest(string Name, LifeFitAI.Domain.Identity.TenantType TenantType, LifeFitAI.Domain.Identity.TenantStatus Status);

public sealed record DeleteBusinessTenantsRequest(Guid[] TenantIds);

public sealed record CreateWorkspaceUserRequest(string Email, string FirstName, string LastName, string RoleName);

public sealed record UpdateWorkspaceUserRequest(string FirstName, string LastName, LifeFitAI.Domain.Identity.UserStatus Status, string RoleName);

public sealed record DeleteWorkspaceUsersRequest(Guid[] UserIds);

public sealed record AssignWorkspacePlanRequest(Guid SubscriptionId);

public sealed record AddMemberRequest(string Email, string RoleName);

public sealed record AssignRoleRequest(Guid UserId, string RoleName);
