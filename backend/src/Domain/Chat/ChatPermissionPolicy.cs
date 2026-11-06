namespace LifeFitAI.Domain.Chat;

public readonly record struct ChatRuleState(string Code, bool Enabled, ChatRelationshipScope Scope, bool AllowImages, bool AllowDocuments);

public readonly record struct ChatAccessDecision(bool Allowed, bool AllowImages, bool AllowDocuments, string? RuleCode);

public static class ChatPermissionPolicy
{
    public static IReadOnlyList<ChatRuleState> Defaults() =>
    [
        Rule(ChatRuleCodes.MemberAssignedTrainer, true, ChatRelationshipScope.AssignedOnly, true, true),
        Rule(ChatRuleCodes.MemberMember, false, ChatRelationshipScope.SameTenant, true, false),
        Rule(ChatRuleCodes.TrainerAssignedMember, true, ChatRelationshipScope.AssignedOnly, true, true),
        Rule(ChatRuleCodes.TrainerUnassignedMember, false, ChatRelationshipScope.SameTenant, false, false),
        Rule(ChatRuleCodes.TrainerTrainer, false, ChatRelationshipScope.SameTenant, true, true),
        Rule(ChatRuleCodes.StaffStaff, false, ChatRelationshipScope.SameTenant, true, true),
        Rule(ChatRuleCodes.StaffTrainer, false, ChatRelationshipScope.SameTenant, true, true),
        Rule(ChatRuleCodes.TrainerStaff, false, ChatRelationshipScope.SameTenant, true, true),
        Rule(ChatRuleCodes.StaffMember, false, ChatRelationshipScope.SameTenant, true, false),
        Rule(ChatRuleCodes.AdminAll, true, ChatRelationshipScope.SameTenant, true, true),
    ];

    public static ChatPartyRole? ResolveParty(IReadOnlyCollection<string> roleNames, bool hasStaffProfile, bool hasTrainerProfile, bool hasMemberProfile)
    {
        var roles = roleNames as ISet<string> ?? new HashSet<string>(roleNames, StringComparer.Ordinal);
        if (roles.Contains("SuperAdmin") || roles.Contains("GymOwner") || roles.Contains("BusinessAdmin") || roles.Contains("GymManager"))
        {
            return ChatPartyRole.Admin;
        }

        if (hasTrainerProfile || roles.Contains("Trainer"))
        {
            return ChatPartyRole.Trainer;
        }

        var personal = roles.Contains("Member") || roles.Contains("PersonalMember") || roles.Contains("PersonalOwner");
        if (hasStaffProfile || roles.Contains("Receptionist") || roles.Contains("Accountant") || roles.Any(role => role is not "Member" and not "PersonalMember" and not "PersonalOwner" and not "Trainer"))
        {
            return ChatPartyRole.Staff;
        }

        if (hasMemberProfile || personal)
        {
            return ChatPartyRole.Member;
        }

        return null;
    }

    public static bool SharesAssignment(Guid? memberProfileId, Guid? assignedTrainerProfileId, Guid? otherTrainerProfileId) =>
        memberProfileId is not null && assignedTrainerProfileId is Guid assigned && otherTrainerProfileId == assigned;

    public static ChatAccessDecision Evaluate(ChatPartyRole sender, ChatPartyRole recipient, bool assigned, IReadOnlyDictionary<string, ChatRuleState> rules)
    {
        if (sender == ChatPartyRole.Admin || recipient == ChatPartyRole.Admin)
        {
            return Decide(ChatRuleCodes.AdminAll, assigned, rules);
        }

        var code = (sender, recipient, assigned) switch
        {
            (ChatPartyRole.Member, ChatPartyRole.Trainer, true) => ChatRuleCodes.MemberAssignedTrainer,
            (ChatPartyRole.Trainer, ChatPartyRole.Member, true) => ChatRuleCodes.TrainerAssignedMember,
            (ChatPartyRole.Trainer, ChatPartyRole.Member, false) => ChatRuleCodes.TrainerUnassignedMember,
            (ChatPartyRole.Member, ChatPartyRole.Member, _) => ChatRuleCodes.MemberMember,
            (ChatPartyRole.Trainer, ChatPartyRole.Trainer, _) => ChatRuleCodes.TrainerTrainer,
            (ChatPartyRole.Staff, ChatPartyRole.Staff, _) => ChatRuleCodes.StaffStaff,
            (ChatPartyRole.Staff, ChatPartyRole.Trainer, _) => ChatRuleCodes.StaffTrainer,
            (ChatPartyRole.Trainer, ChatPartyRole.Staff, _) => ChatRuleCodes.TrainerStaff,
            (ChatPartyRole.Staff, ChatPartyRole.Member, _) => ChatRuleCodes.StaffMember,
            (ChatPartyRole.Member, ChatPartyRole.Staff, _) => ChatRuleCodes.StaffMember,
            _ => null,
        };

        return code is null ? Denied() : Decide(code, assigned, rules);
    }

    public static int CountUnread(Guid userId, DateTime? lastReadAt, IEnumerable<(Guid SenderUserId, DateTime CreatedAt)> messages)
    {
        var count = 0;
        foreach (var message in messages)
        {
            if (message.SenderUserId != userId && (lastReadAt is null || message.CreatedAt > lastReadAt))
            {
                count++;
            }
        }

        return count;
    }

    private static ChatAccessDecision Decide(string code, bool assigned, IReadOnlyDictionary<string, ChatRuleState> rules)
    {
        if (!rules.TryGetValue(code, out var rule))
        {
            rule = Defaults().First(item => item.Code == code);
        }

        var scopeOk = rule.Scope == ChatRelationshipScope.SameTenant || assigned;
        if (!rule.Enabled || !scopeOk)
        {
            return Denied();
        }

        return new ChatAccessDecision(true, rule.AllowImages, rule.AllowDocuments, code);
    }

    private static ChatAccessDecision Denied() => new(false, false, false, null);

    private static ChatRuleState Rule(string code, bool enabled, ChatRelationshipScope scope, bool images, bool documents) =>
        new(code, enabled, scope, images, documents);
}
