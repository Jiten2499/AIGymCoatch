using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym;

public static class GymErrors
{
    public static readonly Error FeatureDisabled = new("gym.feature_disabled", "This feature is not enabled for the current workspace.");
    public static readonly Error BusinessWorkspaceRequired = new("tenant.not_business", "This dashboard is available for a business workspace.");
    public static readonly Error AnalyticsRangeInvalid = new("gym.analytics_range", "The analytics range is not valid for this period.");
    public static readonly Error NotFound = new("gym.not_found", "The requested record was not found in this workspace.");
    public static readonly Error Conflict = new("gym.conflict", "The record conflicts with an existing business record.");
    public static readonly Error InvalidState = new("gym.invalid_state", "The membership cannot change to that state.");
    public static readonly Error FreezeNotAllowed = new("gym.freeze_not_allowed", "This gym does not allow a membership freeze.");
    public static readonly Error FreezeOutsideLimits = new("gym.freeze_outside_limits", "The freeze length is outside the gym's freeze rules.");
    public static readonly Error FreezeLimitReached = new("gym.freeze_limit", "This membership has used its allowed freezes.");
    public static readonly Error ExtensionOutsideLimits = new("gym.extension_outside_limits", "The extension length is outside the gym's extension rules.");
    public static readonly Error ApprovalAlreadyDecided = new("gym.approval_decided", "This request is no longer pending.");
    public static readonly Error ApprovalCommentRequired = new("gym.approval_comment", "A reason is required to reject this request.");
    public static readonly Error InvalidCheckIn = new("gym.invalid_state", "The membership is not valid for check-in.");
    public static readonly Error OpenVisit = new("gym.conflict", "This member already has an open visit.");
    public static readonly Error InvalidToken = new("gym.invalid_state", "The check-in token is invalid.");
    public static readonly Error PasswordRequired = new("identity.password_invalid", "A password is required when creating a new account.");
    public static readonly Error InvalidPhoto = new("gym.invalid_photo", "The profile photo must be a JPEG, PNG, or WebP image under 2 MB.");
    public static readonly Error AppLoginRequired = new("member.app_login_required", "Basic LifeFit app access requires a member email so a login can be linked.");
    public static readonly Error AppSubscriptionRequired = new("member.app_plan_required", "Select a LifeFit app plan when assigning a licensed plan seat.");
    public static readonly Error BasicAccessAlreadyEnabled = new("member.basic_access_exists", "This member already has basic LifeFit app access.");
    public static readonly Error LeaveOverlap = new("gym.leave_overlap", "This leave overlaps an existing pending or approved request.");
    public static readonly Error LeaveDateRange = new("gym.leave_dates", "Leave end date must be on or after the start date.");
    public static readonly Error StaffProfileRequired = new("gym.staff_required", "A staff profile is required to apply for leave.");
    public static readonly Error ActiveSeatBlocksDeletion = new("profile.active_seat", "This user currently has an assigned business seat. Revoke the seat before deleting this profile.");
}
