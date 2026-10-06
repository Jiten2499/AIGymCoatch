using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Identity;

public static class IdentityErrors
{
    public static readonly Error EmailConflict = new("identity.email_conflict", "An account with this email already exists.");
    public static readonly Error PhoneConflict = new("identity.phone_conflict", "An account with this phone already exists.");
    public static readonly Error InvalidCredentials = new("identity.invalid_credentials", "The email or password is incorrect.");
    public static readonly Error UserDisabled = new("identity.user_disabled", "This account is disabled.");
    public static readonly Error RefreshInvalid = new("identity.refresh_invalid", "The refresh token is invalid.");
    public static readonly Error ResetInvalid = new("identity.reset_invalid", "The password reset token is invalid.");
    public static readonly Error PasswordRejected = new("identity.password_invalid", "The password does not meet the password policy.");
    public static readonly Error CurrentPasswordRejected = new("identity.current_password_invalid", "The current password is incorrect.");

    public static readonly Error TenantNotFound = new("tenant.not_found", "The workspace was not found.");
    public static readonly Error NotMember = new("tenant.not_member", "You do not belong to this workspace.");
    public static readonly Error TenantUnavailable = new("tenant.unavailable", "This workspace is not active.");
    public static readonly Error PersonalTypeRejected = new("tenant.type_invalid", "Only business workspaces can be created.");
    public static readonly Error MembershipConflict = new("membership.conflict", "This user is already a member of the workspace.");
    public static readonly Error MemberNotFound = new("membership.not_found", "The workspace member was not found.");
    public static readonly Error OwnerRemovalRejected = new("membership.owner_removal", "The workspace owner cannot be removed.");
    public static readonly Error OwnerRoleRejected = new("membership.owner_role", "The workspace owner's role cannot be changed.");
    public static readonly Error OwnerDisableRejected = new("membership.owner_disable", "The workspace owner cannot be disabled.");
    public static readonly Error SelfRemovalRejected = new("membership.self_removal", "You cannot remove yourself from the workspace.");
    public static readonly Error SelfDisableRejected = new("identity.self_disable", "You cannot disable your own account.");
    public static readonly Error PlatformWorkspaceRejected = new("workspace.platform_rejected", "Users are managed inside a business workspace.");

    public static readonly Error RoleNotFound = new("role.not_found", "The role was not found.");
    public static readonly Error SuperAdminRejected = new("role.super_admin_rejected", "SuperAdmin cannot be assigned through the workspace API.");
    public static readonly Error Forbidden = new("authorization.forbidden", "You do not have permission to perform this action.");
    public static readonly Error LicenseNotFound = new("license.not_found", "This workspace does not have a license.");
}
