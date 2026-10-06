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
    public static readonly Error InvalidCheckIn = new("gym.invalid_state", "The membership is not valid for check-in.");
    public static readonly Error OpenVisit = new("gym.conflict", "This member already has an open visit.");
    public static readonly Error InvalidToken = new("gym.invalid_state", "The check-in token is invalid.");
    public static readonly Error PasswordRequired = new("identity.password_invalid", "A password is required when creating a new account.");
}
