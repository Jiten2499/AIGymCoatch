namespace LifeFitAI.Domain.Gym;

public enum BranchStatus
{
    Active = 1,
    Inactive = 2,
}

public enum StaffType
{
    Trainer = 1,
    Receptionist = 2,
    Manager = 3,
    Other = 4,
}

public enum ProfileStatus
{
    Active = 1,
    Inactive = 2,
    Draft = 3,
    PendingInvitation = 4,
    Suspended = 5,
}

public enum EmploymentType
{
    FullTime = 1,
    PartTime = 2,
    Contract = 3,
    Temporary = 4,
    Intern = 5,
    Freelance = 6,
    VisitingTrainer = 7,
}

public enum MembershipType
{
    Monthly = 1,
    Quarterly = 2,
    HalfYearly = 3,
    Yearly = 4,
    Custom = 5,
}

public enum PackageCategory
{
    Standard = 1,
    Student = 2,
    Women = 3,
    Corporate = 4,
    Couple = 5,
}

public enum PackageDurationUnit
{
    Day = 1,
    Month = 2,
}

public enum GymMembershipStatus
{
    Pending = 1,
    Active = 2,
    Expired = 3,
    Frozen = 4,
    Cancelled = 5,
    Suspended = 6,
}

public enum CheckInMethod
{
    QR = 1,
    Reception = 2,
    Manual = 3,
    Other = 4,
}

public enum MembershipAction
{
    Created = 1,
    Renewed = 2,
    Frozen = 3,
    Unfrozen = 4,
    Cancelled = 5,
    Expired = 6,
    Extended = 7,
}
