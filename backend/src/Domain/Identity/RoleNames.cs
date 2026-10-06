namespace LifeFitAI.Domain.Identity;

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string GymOwner = "GymOwner";
    public const string GymManager = "GymManager";
    public const string Trainer = "Trainer";
    public const string Receptionist = "Receptionist";
    public const string Member = "Member";
    public const string PersonalOwner = "PersonalOwner";
    public const string PersonalMember = "PersonalMember";

    public static string OwnerRoleFor(TenantType tenantType) =>
        tenantType == TenantType.Platform ? SuperAdmin : GymOwner;

    public static bool IsBusiness(TenantType tenantType) => tenantType != TenantType.Platform;
}
