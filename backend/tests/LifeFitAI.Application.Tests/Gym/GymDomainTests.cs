using FluentAssertions;
using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class GymDomainTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    [Fact]
    public void Membership_CreatePendingWhenStartIsInTheFuture()
    {
        var membership = Membership(Today.AddDays(3), 30);

        membership.Status.Should().Be(GymMembershipStatus.Pending);
        membership.CanCheckIn(Today).Should().BeFalse();
        membership.EndDate.Should().Be(Today.AddDays(32));
    }

    [Fact]
    public void Membership_LifecyclePreservesTheRecord()
    {
        var membership = Membership(Today.AddDays(-10), 30);

        membership.CanCheckIn(Today).Should().BeTrue();
        membership.Freeze(Today).Should().BeTrue();
        membership.Status.Should().Be(GymMembershipStatus.Frozen);
        membership.CanCheckIn(Today).Should().BeFalse();
        membership.Freeze(Today).Should().BeFalse();
        membership.Unfreeze(Today).Should().BeTrue();
        membership.Status.Should().Be(GymMembershipStatus.Active);
        membership.Renew(Today, 30, true).Should().BeTrue();
        membership.Status.Should().Be(GymMembershipStatus.Active);
        membership.AutoRenew.Should().BeTrue();
        membership.EndDate.Should().Be(Today.AddDays(49));
        membership.Cancel().Should().BeTrue();
        membership.Renew(Today, 30, false).Should().BeFalse();
        membership.Status.Should().Be(GymMembershipStatus.Cancelled);
        membership.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Membership_ExpireAndRenewFromExpired()
    {
        var membership = Membership(Today.AddDays(-40), 10);
        membership.Expire(Today).Should().BeTrue();
        membership.CanCheckIn(Today).Should().BeFalse();
        membership.Expire(Today).Should().BeFalse();
        membership.Renew(Today, 30, false).Should().BeTrue();
        membership.Status.Should().Be(GymMembershipStatus.Active);
        membership.StartDate.Should().Be(Today);
    }

    [Fact]
    public void Membership_UnfreezeAfterEndDateExpires()
    {
        var membership = Membership(Today.AddDays(-40), 10);
        membership.Status.Should().Be(GymMembershipStatus.Active);
        typeof(GymMembership).GetProperty(nameof(GymMembership.Status))!.SetValue(membership, GymMembershipStatus.Frozen);

        membership.Unfreeze(Today).Should().BeTrue();
        membership.Status.Should().Be(GymMembershipStatus.Expired);
    }

    [Fact]
    public void Attendance_AllowsAnotherVisitAfterCheckout()
    {
        var first = AttendanceRecord.CheckIn(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CheckInMethod.QR, DateTime.UtcNow);
        first.IsOpen.Should().BeTrue();
        first.CheckOut(DateTime.UtcNow.AddHours(1)).Should().BeTrue();
        first.CheckOut(DateTime.UtcNow.AddHours(2)).Should().BeFalse();
        first.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void QrToken_ExpiresAndCannotBeReplayed()
    {
        var now = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
        var token = QrCheckInToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", now, now.AddMinutes(10), Guid.NewGuid());
        token.IsUsable(now.AddMinutes(9)).Should().BeTrue();
        token.IsUsable(now.AddMinutes(10)).Should().BeFalse();
        token.MarkUsed(now.AddMinutes(1));
        token.IsUsable(now.AddMinutes(2)).Should().BeFalse();
        token.TokenHash.Should().Be("hash");
    }

    [Fact]
    public void Branch_CodeStaysStableAndDeactivateDoesNotDelete()
    {
        var branch = Branch.Create(Guid.NewGuid(), Guid.NewGuid(), "Main", "MAIN", "UTC");
        branch.Update("Main Hall", "1 Street", "Pune", "MH", "IN", "411001", null, null, "UTC");
        branch.Code.Should().Be("MAIN");
        branch.Deactivate();
        branch.Status.Should().Be(BranchStatus.Inactive);
        branch.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Plan_DeactivateKeepsTheRow()
    {
        var plan = MembershipPlan.Create(Guid.NewGuid(), Guid.NewGuid(), "Monthly", null, 30, MembershipType.Monthly, null, 49.5m, "USD");
        plan.Deactivate();
        plan.IsActive.Should().BeFalse();
        plan.Price.Should().Be(49.5m);
    }

    private static GymMembership Membership(DateOnly start, int days) =>
        GymMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "G12345678", start, days, Today, false, null);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(500, 20)]
    [InlineData(25, 25)]
    public void PageQuery_ClampsSize(int requested, int expected)
    {
        new LifeFitAI.Application.Gym.PageQuery(1, requested).NormalizedSize.Should().Be(expected);
    }
}
