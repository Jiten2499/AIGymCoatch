using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Gym.Memberships;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class ApprovalInboxTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _other = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _user = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 8, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Decision_CanHappenOnce()
    {
        var request = Sample(PermissionCodes.MembershipFreeze);
        request.Approve(_user, "Sumit", _utc, null).Should().BeTrue();
        request.Approve(_user, "Sumit", _utc, null).Should().BeFalse();
        request.Reject(_user, "Sumit", _utc, "No").Should().BeFalse();
        request.Status.Should().Be(ApprovalRequestStatus.Approved);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var request = Sample(PermissionCodes.MembershipRenew);
        request.Reject(_user, "Sumit", _utc, " ").Should().BeFalse();
        request.Status.Should().Be(ApprovalRequestStatus.Pending);
        new DecideApprovalRequestCommandValidator().Validate(new DecideApprovalRequestCommand(request.Id, false, "")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Inbox_ShowsOnlyRequestsTheUserCanApprove()
    {
        var requests = Substitute.For<IApprovalRequestRepository>();
        requests.SearchAsync(_tenant, Arg.Is<IReadOnlyList<string>>(items => items.Count == 1 && items[0] == PermissionCodes.MembershipFreeze), Arg.Any<ApprovalListQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PageResult<ApprovalRequestListItem>([new ApprovalRequestListItem(Guid.NewGuid(), ApprovalRequestType.MembershipFreeze, "Membership freeze", "7 days", "Amit", _utc, "Rahul", null, null, 7, ApprovalRequestStatus.Pending)], 1, 20, 1));
        var handler = Queries(requests, freeze: true, renew: false);

        var page = await handler.Handle(new ListApprovalRequestsQuery(new ApprovalListQuery(ApprovalRequestStatus.Pending, null, null, null, null, 1, 20, null)), CancellationToken.None);

        page.IsSuccess.Should().BeTrue();
        page.Value.Items.Should().ContainSingle(item => item.RequestType == ApprovalRequestType.MembershipFreeze);
        page.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Inbox_HidesRequestsFromAUserWithoutApprovalPermission()
    {
        var handler = Queries(Substitute.For<IApprovalRequestRepository>(), freeze: false, renew: false);
        var page = await handler.Handle(new ListApprovalRequestsQuery(new ApprovalListQuery(ApprovalRequestStatus.Pending, null, null, null, null, 1, 20, null)), CancellationToken.None);
        page.Error.Should().Be(IdentityErrors.Forbidden);
    }

    [Fact]
    public async Task Detail_DoesNotCrossTenants()
    {
        var requests = Substitute.For<IApprovalRequestRepository>();
        requests.GetAsync(_tenant, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ApprovalRequest?)null);
        var handler = Queries(requests, freeze: true, renew: true);
        var missing = await handler.Handle(new GetApprovalRequestQuery(Guid.NewGuid()), CancellationToken.None);
        missing.Error.Should().Be(GymErrors.NotFound);
        await requests.DidNotReceive().GetAsync(_other, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_AppliesAFreezeAndRejectsASecondDecision()
    {
        var request = Sample(PermissionCodes.MembershipFreeze);
        var membership = GymMembership.Create(request.SourceId, _tenant, Guid.NewGuid(), null, Guid.NewGuid(), "G12345678", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 8), false, null);
        var requests = Substitute.For<IApprovalRequestRepository>();
        requests.GetAsync(_tenant, request.Id, Arg.Any<CancellationToken>()).Returns(request);
        var memberships = Substitute.For<IMembershipRepository>();
        memberships.GetMembershipAsync(_tenant, request.SourceId, Arg.Any<CancellationToken>()).Returns(membership);
        var handler = Decisions(requests, memberships);

        var approved = await handler.Handle(new DecideApprovalRequestCommand(request.Id, true, null), CancellationToken.None);
        approved.IsSuccess.Should().BeTrue();
        approved.Value.Status.Should().Be(ApprovalRequestStatus.Approved);
        approved.Value.History.Should().Contain(entry => entry.Action == "Requested").And.Contain(entry => entry.Action == "Approved");
        membership.Status.Should().Be(GymMembershipStatus.Frozen);
        membership.EndDate.Should().Be(new DateOnly(2026, 11, 7));

        var again = await handler.Handle(new DecideApprovalRequestCommand(request.Id, true, null), CancellationToken.None);
        again.Error.Should().Be(GymErrors.ApprovalAlreadyDecided);
    }

    [Fact]
    public async Task Reject_RecordsTheComment()
    {
        var request = Sample(PermissionCodes.MembershipRenew);
        var requests = Substitute.For<IApprovalRequestRepository>();
        requests.GetAsync(_tenant, request.Id, Arg.Any<CancellationToken>()).Returns(request);
        var handler = Decisions(requests, Substitute.For<IMembershipRepository>());
        var rejected = await handler.Handle(new DecideApprovalRequestCommand(request.Id, false, "Not this month"), CancellationToken.None);
        rejected.IsSuccess.Should().BeTrue();
        rejected.Value.Status.Should().Be(ApprovalRequestStatus.Rejected);
        rejected.Value.DecisionComment.Should().Be("Not this month");
    }

    private ApprovalRequest Sample(string permission)
    {
        var type = permission == PermissionCodes.MembershipFreeze ? ApprovalRequestType.MembershipFreeze : ApprovalRequestType.MembershipExtension;
        return ApprovalRequest.Create(Guid.NewGuid(), _tenant, type, Guid.NewGuid(), permission, type == ApprovalRequestType.MembershipFreeze ? "Membership freeze" : "Membership extension", "7 days", "Travel", _user, "Amit", Guid.NewGuid(), "Rahul Sharma", 7, new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 7), null, "INR");
    }

    private ApprovalQueryHandler Queries(IApprovalRequestRepository requests, bool freeze, bool renew)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_user, _tenant, PermissionCodes.MembershipFreeze, Arg.Any<CancellationToken>()).Returns(freeze);
        permissions.HasPermissionAsync(_user, _tenant, PermissionCodes.MembershipRenew, Arg.Any<CancellationToken>()).Returns(renew);
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), FeatureCodes.MembershipManagement, Arg.Any<CancellationToken>()).Returns(call =>
            ((freeze && call.ArgAt<string>(0) == PermissionCodes.MembershipFreeze) || (renew && call.ArgAt<string>(0) == PermissionCodes.MembershipRenew))
                ? Result.Success()
                : Result.Failure(IdentityErrors.Forbidden));
        return new ApprovalQueryHandler(guard, Actor(), permissions, requests);
    }

    private DecideApprovalRequestCommandHandler Decisions(IApprovalRequestRepository requests, IMembershipRepository memberships)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_user, _tenant, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var users = Substitute.For<IUserAccountRepository>();
        var unit = Substitute.For<IUnitOfWork>();
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        return new DecideApprovalRequestCommandHandler(guard, Actor(), permissions, requests, memberships, Substitute.For<IBusinessConfigRepository>(), Substitute.For<LifeFitAI.Application.Gym.Trainers.ITrainerRewardStore>(), Substitute.For<LifeFitAI.Application.Gym.Leave.ILeaveStore>(), Substitute.For<LifeFitAI.Application.Fitness.IChallengeRepository>(), users, Substitute.For<IAuditLogRepository>(), unit, Clock(), Substitute.For<IClientContext>());
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_user);
        actor.TenantId.Returns(_tenant);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return clock;
    }
}
