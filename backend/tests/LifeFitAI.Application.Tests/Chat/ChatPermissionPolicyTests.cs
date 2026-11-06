using FluentAssertions;
using LifeFitAI.Domain.Chat;

namespace LifeFitAI.Application.Tests.Chat;

public sealed class ChatPermissionPolicyTests
{
    private static IReadOnlyDictionary<string, ChatRuleState> Rules(params ChatRuleState[] overrides)
    {
        var map = ChatPermissionPolicy.Defaults().ToDictionary(item => item.Code, item => item);
        foreach (var rule in overrides)
        {
            map[rule.Code] = rule;
        }

        return map;
    }

    [Fact]
    public void Member_CanMessageAssignedTrainer_ByDefault()
    {
        var decision = ChatPermissionPolicy.Evaluate(ChatPartyRole.Member, ChatPartyRole.Trainer, true, Rules());
        decision.Allowed.Should().BeTrue();
        decision.RuleCode.Should().Be(ChatRuleCodes.MemberAssignedTrainer);
    }

    [Fact]
    public void Member_CannotMessageUnassignedTrainer()
    {
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Member, ChatPartyRole.Trainer, false, Rules()).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Member_CanMessageMember_OnlyWhenEnabled()
    {
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Member, ChatPartyRole.Member, false, Rules()).Allowed.Should().BeFalse();
        var enabled = Rules(new ChatRuleState(ChatRuleCodes.MemberMember, true, ChatRelationshipScope.SameTenant, true, false));
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Member, ChatPartyRole.Member, false, enabled).Allowed.Should().BeTrue();
    }

    [Fact]
    public void Trainer_CanMessageAssignedMember_AndNotStrangers()
    {
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Trainer, ChatPartyRole.Member, true, Rules()).Allowed.Should().BeTrue();
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Trainer, ChatPartyRole.Member, false, Rules()).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Admin_CanMessageTenantUsers_WithoutReadingPrivateThreads()
    {
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Admin, ChatPartyRole.Member, false, Rules()).Allowed.Should().BeTrue();
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Trainer, ChatPartyRole.Admin, false, Rules()).Allowed.Should().BeTrue();
        var denied = Rules(new ChatRuleState(ChatRuleCodes.AdminAll, false, ChatRelationshipScope.SameTenant, true, true));
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Admin, ChatPartyRole.Member, false, denied).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Staff_WithSameGymRules_CanReachStaffTrainersAndMembers()
    {
        var rules = Rules(
            new ChatRuleState(ChatRuleCodes.StaffStaff, true, ChatRelationshipScope.SameTenant, true, true),
            new ChatRuleState(ChatRuleCodes.StaffTrainer, true, ChatRelationshipScope.SameTenant, true, true),
            new ChatRuleState(ChatRuleCodes.StaffMember, true, ChatRelationshipScope.SameTenant, true, false));
        ChatPermissionPolicy.ResolveParty(["Front desk lead"], false, false, false).Should().Be(ChatPartyRole.Staff);
        ChatPermissionPolicy.ResolveParty(["Trainer"], false, true, false).Should().Be(ChatPartyRole.Trainer);
        ChatPermissionPolicy.ResolveParty(["Member"], false, false, true).Should().Be(ChatPartyRole.Member);
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Staff, ChatPartyRole.Staff, false, rules).Allowed.Should().BeTrue();
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Staff, ChatPartyRole.Trainer, false, rules).Allowed.Should().BeTrue();
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Staff, ChatPartyRole.Member, false, rules).Allowed.Should().BeTrue();
    }

    [Fact]
    public void Staff_And_Trainer_Rules_AreIndependent()
    {
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Staff, ChatPartyRole.Trainer, false, Rules()).Allowed.Should().BeFalse();
        var staffToTrainer = Rules(new ChatRuleState(ChatRuleCodes.StaffTrainer, true, ChatRelationshipScope.SameTenant, true, true));
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Staff, ChatPartyRole.Trainer, false, staffToTrainer).Allowed.Should().BeTrue();
        ChatPermissionPolicy.Evaluate(ChatPartyRole.Trainer, ChatPartyRole.Staff, false, staffToTrainer).Allowed.Should().BeFalse();
    }

    [Fact]
    public void DirectPairKey_IsStable_AndUnreadCountsIgnoreOwnMessages()
    {
        var left = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
        var right = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
        ChatConversation.DirectPairKey(left, right).Should().Be(ChatConversation.DirectPairKey(right, left));
        var at = new DateTime(2026, 10, 31, 4, 0, 0, DateTimeKind.Utc);
        ChatPermissionPolicy.CountUnread(left, at, [(right, at.AddMinutes(1)), (left, at.AddMinutes(2)), (right, at.AddMinutes(-1))]).Should().Be(1);
    }

    [Fact]
    public void Assignment_RequiresTheLinkedTrainerProfile()
    {
        var member = Guid.NewGuid();
        var trainer = Guid.NewGuid();
        ChatPermissionPolicy.SharesAssignment(member, trainer, trainer).Should().BeTrue();
        ChatPermissionPolicy.SharesAssignment(member, trainer, Guid.NewGuid()).Should().BeFalse();
    }
}
