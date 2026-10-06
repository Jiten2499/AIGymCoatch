namespace LifeFitAI.Domain.Identity;

public enum UserStatus
{
    Active = 1,
    Disabled = 2,
}

public enum TenantType
{
    Platform = 1,
    Gym = 2,
    YogaStudio = 3,
    CrossFit = 4,
    Physio = 5,
    Corporate = 6,
}

public enum TenantStatus
{
    Active = 1,
    Suspended = 2,
    Inactive = 3,
}

public enum MembershipStatus
{
    Active = 1,
    Removed = 2,
}

public enum LicensePlan
{
    Free = 1,
    Basic = 2,
    Pro = 3,
    Enterprise = 4,
}

public enum LicenseStatus
{
    Active = 1,
    Expired = 2,
    Suspended = 3,
    Cancelled = 4,
    Scheduled = 5,
    Revoked = 6,
}

public enum PlanStatus
{
    Draft = 1,
    Active = 2,
    Inactive = 3,
    Retired = 4,
}

public enum FeatureScope
{
    Tenant = 1,
    User = 2,
}

public enum LicenseAssignmentStatus
{
    Active = 1,
    Revoked = 2,
}

public enum SubscriberType
{
    Tenant = 1,
    User = 2,
}

public enum SubscriptionStatus
{
    Active = 1,
    Expired = 2,
    Suspended = 3,
    Cancelled = 4,
}

public enum SeatStatus
{
    Active = 1,
    Released = 2,
}
