namespace LifeFitAI.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, string? Phone);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record ActivationPreviewRequest(string Token);

public sealed record SwitchTenantRequest(Guid TenantId);

public sealed record CreateTenantRequest(string Name, LifeFitAI.Domain.Identity.TenantType TenantType);

public sealed record CreateBusinessTenantRequest(string Name, LifeFitAI.Domain.Identity.TenantType TenantType, string OwnerEmail, string OwnerFirstName, string OwnerLastName, bool SendActivationLink = false);

public sealed record UpdateBusinessTenantRequest(string Name, LifeFitAI.Domain.Identity.TenantType TenantType, LifeFitAI.Domain.Identity.TenantStatus Status);

public sealed record DeleteBusinessTenantsRequest(Guid[] TenantIds);

public sealed record CreateWorkspaceUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string RoleName,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    string? Gender = null,
    string? Address = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? EmployeeCode = null,
    string? Designation = null,
    string? Department = null,
    LifeFitAI.Domain.Gym.EmploymentType? EmploymentType = null,
    DateOnly? JoiningDate = null,
    Guid? BranchId = null,
    Guid? ReportingManagerUserId = null,
    string? EmploymentNotes = null,
    string? JobDescription = null,
    Guid[]? AuthorizedBranchIds = null,
    Guid? SubscriptionId = null,
    LifeFitAI.Application.Authorization.AccessScopeInput[]? ScopeOverrides = null,
    string[]? PermissionOverrides = null,
    string? Specialization = null,
    string? Certification = null,
    int? ExperienceYears = null,
    string? TrainingExpertise = null,
    bool LinkExistingUser = false,
    bool SaveAsDraft = false,
    bool SendInvitation = false);

public sealed record UpdateWorkspaceUserRequest(
    string FirstName,
    string LastName,
    LifeFitAI.Domain.Identity.UserStatus Status,
    string RoleName,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    string? Gender = null,
    string? Address = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? EmployeeCode = null,
    string? Designation = null,
    string? Department = null,
    LifeFitAI.Domain.Gym.EmploymentType? EmploymentType = null,
    DateOnly? JoiningDate = null,
    Guid? BranchId = null,
    Guid? ReportingManagerUserId = null,
    string? EmploymentNotes = null,
    string? JobDescription = null,
    Guid[]? AuthorizedBranchIds = null,
    Guid? SubscriptionId = null,
    LifeFitAI.Application.Authorization.AccessScopeInput[]? ScopeOverrides = null,
    string[]? PermissionOverrides = null,
    string? Specialization = null,
    string? Certification = null,
    int? ExperienceYears = null,
    string? TrainingExpertise = null,
    string? EmploymentStatus = null);

public sealed record DeleteWorkspaceUsersRequest(Guid[] UserIds);

public sealed record AssignWorkspacePlanRequest(Guid SubscriptionId);

public sealed record RevokeWorkspacePlanSeatsRequest(Guid[] SeatIds);

public sealed record AssignWorkspacePlanUsersRequest(Guid[] UserIds);

public sealed record AddMemberRequest(string Email, string RoleName);

public sealed record AssignRoleRequest(Guid UserId, string RoleName);
