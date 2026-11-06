namespace LifeFitAI.Domain.Chat;

public enum ChatConversationType
{
    Direct = 1,
}

public enum ChatMessageType
{
    Text = 1,
    Image = 2,
    Document = 3,
}

public enum ChatPartyRole
{
    Member = 1,
    Trainer = 2,
    Staff = 3,
    Admin = 4,
}

public enum ChatRelationshipScope
{
    AssignedOnly = 1,
    SameTenant = 2,
}

public static class ChatRuleCodes
{
    public const string MemberAssignedTrainer = "MemberAssignedTrainer";
    public const string MemberMember = "MemberMember";
    public const string TrainerAssignedMember = "TrainerAssignedMember";
    public const string TrainerUnassignedMember = "TrainerUnassignedMember";
    public const string TrainerTrainer = "TrainerTrainer";
    public const string StaffStaff = "StaffStaff";
    public const string StaffTrainer = "StaffTrainer";
    public const string TrainerStaff = "TrainerStaff";
    public const string StaffMember = "StaffMember";
    public const string AdminAll = "AdminAll";

    public static readonly string[] All =
    [
        MemberAssignedTrainer,
        MemberMember,
        TrainerAssignedMember,
        TrainerUnassignedMember,
        TrainerTrainer,
        StaffStaff,
        StaffTrainer,
        TrainerStaff,
        StaffMember,
        AdminAll,
    ];
}
