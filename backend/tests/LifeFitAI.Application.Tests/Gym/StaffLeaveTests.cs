using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Application.Gym.Leave;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class StaffLeaveTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly Guid _staffId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-ddddddddddd1");
    private readonly DateTime _utc = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Duration_And_Overlap_UseInclusiveDates()
    {
        StaffLeaveRequest.CalculateDuration(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3)).Should().Be(3);
        StaffLeaveRequest.RangesOverlap(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7)).Should().BeTrue();
        StaffLeaveRequest.RangesOverlap(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7)).Should().BeFalse();
    }

    [Fact]
    public void Leave_Cannot_Be_Rewritten_After_Decision()
    {
        var leave = StaffLeaveRequest.Create(Guid.NewGuid(), _tenantA, _staffId, _userId, LeaveType.Annual, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22), "Family trip", null, _utc);
        leave.Approve(Guid.NewGuid(), "ok", _utc).Should().BeTrue();
        leave.Cancel(_utc).Should().BeFalse();
        leave.Reject(Guid.NewGuid(), "too late", _utc).Should().BeFalse();
    }

    [Fact]
    public async Task Apply_RejectsOverlap_AndRequiresStaffProfile()
    {
        var store = Substitute.For<ILeaveStore>();
        store.HasOverlapAsync(_tenantA, _staffId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>()).Returns(true);
        var people = Substitute.For<IPeopleRepository>();
        people.GetStaffByUserAsync(_tenantA, _userId, Arg.Any<CancellationToken>()).Returns(Staff(_staffId));
        var handler = new LeaveCommandHandler(Allow(PermissionCodes.LeaveApply), Actor(_tenantA), store, people, Substitute.For<IApprovalRequestRepository>(), Users(), Notifications(), Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var overlap = await handler.Handle(new ApplyStaffLeaveCommand(LeaveType.Sick, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21), "Fever", null), CancellationToken.None);
        overlap.Error.Should().Be(GymErrors.LeaveOverlap);

        people.GetStaffByUserAsync(_tenantA, _userId, Arg.Any<CancellationToken>()).Returns((StaffProfile?)null);
        var missing = await handler.Handle(new ApplyStaffLeaveCommand(LeaveType.Sick, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21), "Fever", null), CancellationToken.None);
        missing.Error.Should().Be(GymErrors.StaffProfileRequired);
    }

    [Fact]
    public async Task GetLeave_IsTenantIsolated()
    {
        var leave = StaffLeaveRequest.Create(Guid.NewGuid(), _tenantA, _staffId, _userId, LeaveType.Casual, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20), "Errand", null, _utc);
        var store = Substitute.For<ILeaveStore>();
        store.GetAsync(_tenantB, leave.Id, Arg.Any<CancellationToken>()).Returns((StaffLeaveRequest?)null);
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_userId, _tenantB, PermissionCodes.LeaveRead, Arg.Any<CancellationToken>()).Returns(true);
        var queries = new LeaveQueryHandler(Allow(PermissionCodes.LeaveApply), Actor(_tenantB), store, Substitute.For<IPeopleRepository>(), Users(), permissions);
        var result = await queries.Handle(new GetStaffLeaveQuery(leave.Id), CancellationToken.None);
        result.Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public async Task ApproveLeave_LocksRequest_AndNotifies()
    {
        var leave = StaffLeaveRequest.Create(Guid.NewGuid(), _tenantA, _staffId, _userId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3), "Travel", null, _utc);
        var approval = ApprovalRequest.Create(Guid.NewGuid(), _tenantA, ApprovalRequestType.StaffLeave, leave.Id, PermissionCodes.LeaveApprove, "Staff leave", "summary", leave.Reason, _userId, "Sam", null, "Sam", leave.DurationDays, leave.StartDate, leave.EndDate, null, null);
        leave.LinkApproval(approval.Id);
        var leaves = Substitute.For<ILeaveStore>();
        leaves.GetAsync(_tenantA, leave.Id, Arg.Any<CancellationToken>()).Returns(leave);
        var requests = Substitute.For<IApprovalRequestRepository>();
        requests.GetAsync(_tenantA, approval.Id, Arg.Any<CancellationToken>()).Returns(approval);
        var unit = Unit();
        var notifications = Notifications();
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_userId, _tenantA, PermissionCodes.LeaveApprove, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new DecideApprovalRequestCommandHandler(
            Allow(PermissionCodes.LeaveApprove),
            Actor(_tenantA),
            permissions,
            requests,
            Substitute.For<IMembershipRepository>(),
            Substitute.For<LifeFitAI.Application.Gym.Configuration.IBusinessConfigRepository>(),
            Substitute.For<LifeFitAI.Application.Gym.Trainers.ITrainerRewardStore>(),
            leaves,
            notifications,
            Users(),
            Substitute.For<IAuditLogRepository>(),
            unit,
            Clock(),
            Substitute.For<IClientContext>());
        var result = await handler.Handle(new DecideApprovalRequestCommand(approval.Id, true, "Enjoy"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        leave.Status.Should().Be(LeaveRequestStatus.Approved);
        await notifications.Received().AddNotificationAsync(Arg.Is<Notification>(item => item.Type == NotificationType.StaffLeaveUpdate), Arg.Any<CancellationToken>());
    }

    private static StaffProfile Staff(Guid id)
    {
        var profile = StaffProfile.Create(id, Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1"), Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1"), "E1", null, StaffType.Manager, new DateOnly(2024, 1, 1));
        return profile;
    }

    private static IOperationGuard Allow(string permission)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), FeatureCodes.StaffLeave, Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string>(0) == permission ? Result.Success() : Result.Failure(IdentityErrors.Forbidden));
        return guard;
    }

    private ICurrentActor Actor(Guid tenantId)
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(tenantId);
        actor.UserId.Returns(_userId);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return clock;
    }

    private static IUnitOfWork Unit()
    {
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        return unit;
    }

    private static IUserAccountRepository Users()
    {
        var users = Substitute.For<IUserAccountRepository>();
        users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        return users;
    }

    private static IChallengeRepository Notifications() => Substitute.For<IChallengeRepository>();
}
