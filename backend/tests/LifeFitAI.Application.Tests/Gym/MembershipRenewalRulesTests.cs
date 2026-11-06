using System.Reflection;
using FluentAssertions;
using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class MembershipRenewalRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void EarlyRenewalStartsTheDayAfterTheCurrentEndAndKeepsHistory()
    {
        var current = Membership(new DateOnly(2026, 10, 1), 31);
        var originalEnd = current.EndDate;
        current.ApplyIncludedSessions(4);
        current.TryConsumeIncludedSession(Today);

        var successor = MembershipRenewalRules.CreateSuccessor(current, Plan(current.TenantId, 30, PackageDurationUnit.Day, 8), Today, new DateOnly(2026, 11, 1), true, Guid.NewGuid(), "GNEW00001", false, null);

        MembershipRenewalRules.DefaultStart(current, Today).Should().Be(new DateOnly(2026, 11, 1));
        successor!.StartDate.Should().Be(new DateOnly(2026, 11, 1));
        successor.EndDate.Should().Be(new DateOnly(2026, 11, 30));
        successor.IncludedPtSessions.Should().Be(8);
        successor.PtSessionsRemaining.Should().Be(8);
        current.EndDate.Should().Be(originalEnd);
        current.PtSessionsRemaining.Should().Be(3);
        current.Status.Should().Be(GymMembershipStatus.Active);
    }

    [Fact]
    public void ExpiredRenewalStartsOnTheSuppliedTenantDate()
    {
        var current = Membership(new DateOnly(2026, 8, 1), 10);
        current.Expire(Today);

        MembershipRenewalRules.DefaultStart(current, Today).Should().Be(Today);
        var successor = MembershipRenewalRules.CreateSuccessor(current, Plan(current.TenantId, 1, PackageDurationUnit.Month, 0), Today, Today, false, Guid.NewGuid(), "GNEW00002", false, null);
        successor!.StartDate.Should().Be(Today);
        successor.EndDate.Should().Be(new DateOnly(2026, 11, 9));
        current.Status.Should().Be(GymMembershipStatus.Expired);
    }

    [Fact]
    public void PackageChangeRecalculatesACalendarEndDate()
    {
        var current = Membership(new DateOnly(2026, 10, 1), 31);
        var start = new DateOnly(2026, 11, 1);
        var days = MembershipRenewalRules.CreateSuccessor(current, Plan(current.TenantId, 30, PackageDurationUnit.Day, 0), Today, start, true, Guid.NewGuid(), "GNEW00003", false, null);
        var months = MembershipRenewalRules.CreateSuccessor(current, Plan(current.TenantId, 1, PackageDurationUnit.Month, 0), Today, start, true, Guid.NewGuid(), "GNEW00004", false, null);

        days!.EndDate.Should().Be(new DateOnly(2026, 11, 30));
        months!.EndDate.Should().Be(new DateOnly(2026, 12, 1));
        MembershipCalendar.InclusiveEnd(new DateOnly(2026, 1, 31), 1, PackageDurationUnit.Month).Should().Be(new DateOnly(2026, 2, 28));
        MembershipCalendar.InclusiveEnd(new DateOnly(2024, 1, 31), 1, PackageDurationUnit.Month).Should().Be(new DateOnly(2024, 2, 29));
        MembershipCalendar.InclusiveEnd(new DateOnly(2026, 1, 31), 12, PackageDurationUnit.Month).Should().Be(new DateOnly(2027, 1, 31));
    }

    [Fact]
    public void InactivePlanAndIneligibleStatesCannotRenew()
    {
        var current = Membership(new DateOnly(2026, 10, 1), 31);
        var inactive = Plan(current.TenantId, 30, PackageDurationUnit.Day, 0);
        inactive.Deactivate();
        var otherTenant = Plan(Guid.NewGuid(), 30, PackageDurationUnit.Day, 0);

        MembershipRenewalRules.CreateSuccessor(current, inactive, Today, new DateOnly(2026, 11, 1), true, Guid.NewGuid(), "GNEW00006", false, null).Should().BeNull();
        MembershipRenewalRules.CreateSuccessor(current, otherTenant, Today, new DateOnly(2026, 11, 1), true, Guid.NewGuid(), "GNEW00007", false, null).Should().BeNull();
        MembershipRenewalRules.CreateSuccessor(current, Plan(current.TenantId, 30, PackageDurationUnit.Day, 0), Today, new DateOnly(2026, 10, 31), true, Guid.NewGuid(), "GNEW00008", false, null).Should().BeNull();
        MembershipRenewalRules.CreateSuccessor(current, Plan(current.TenantId, 30, PackageDurationUnit.Day, 0), Today, new DateOnly(2026, 11, 1), false, Guid.NewGuid(), "GNEW00009", false, null).Should().BeNull();

        current.Cancel();
        MembershipRenewalRules.DefaultStart(current, Today).Should().BeNull();
        MembershipRenewalRules.DefaultStart(Membership(Today.AddDays(3), 30), Today).Should().BeNull();
        MembershipRenewalRules.DefaultStart(SetStatus(Membership(new DateOnly(2026, 10, 1), 31), GymMembershipStatus.Suspended), Today).Should().BeNull();
    }

    [Fact]
    public void FrozenMembershipRenewsAfterItsEndWithoutChangingTheFrozenPeriod()
    {
        var current = SetStatus(Membership(new DateOnly(2026, 10, 1), 31), GymMembershipStatus.Frozen);
        var end = current.EndDate;

        var successor = MembershipRenewalRules.CreateSuccessor(current, Plan(current.TenantId, 30, PackageDurationUnit.Day, 2), Today, end.AddDays(1), true, Guid.NewGuid(), "GNEW00010", false, null);

        successor!.StartDate.Should().Be(end.AddDays(1));
        successor.IncludedPtSessions.Should().Be(2);
        current.Status.Should().Be(GymMembershipStatus.Frozen);
        current.EndDate.Should().Be(end);
    }

    private static GymMembership Membership(DateOnly start, int days) =>
        GymMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), "GCURRENT1", start, days, Today, false, null);

    private static MembershipPlan Plan(Guid tenantId, int count, PackageDurationUnit unit, int sessions) =>
        MembershipPlan.Create(Guid.NewGuid(), tenantId, "Plan", null, count, MembershipType.Monthly, null, 100m, "USD", durationUnit: unit, durationCount: count, includedPtSessions: sessions);

    private static GymMembership SetStatus(GymMembership membership, GymMembershipStatus status)
    {
        typeof(GymMembership).GetProperty(nameof(GymMembership.Status))!.SetValue(membership, status);
        return membership;
    }
}
