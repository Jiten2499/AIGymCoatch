using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Attendance;
using LifeFitAI.Application.Gym.Branches;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Gym.Memberships;
using LifeFitAI.Application.Gym.Staffing;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class GymAccessTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Guard_RejectsMissingPermissionAndDisabledFeature()
    {
        var permissions = Substitute.For<IPermissionService>();
        var features = Substitute.For<LifeFitAI.Application.Licensing.IFeatureFlagService>();
        var actor = Actor();
        permissions.HasPermissionAsync(_userId, _tenantId, PermissionCodes.MemberRead, Arg.Any<CancellationToken>()).Returns(false);
        var guard = new OperationGuard(actor, permissions, features);

        var denied = await guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, CancellationToken.None);

        denied.IsFailure.Should().BeTrue();
        denied.Error.Code.Should().Be("authorization.forbidden");
        await features.DidNotReceive().IsEnabledAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        permissions.HasPermissionAsync(_userId, _tenantId, PermissionCodes.MemberRead, Arg.Any<CancellationToken>()).Returns(true);
        features.IsEnabledAsync(_tenantId, FeatureCodes.MemberManagement, Arg.Any<CancellationToken>()).Returns(false);
        var disabled = await guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, CancellationToken.None);
        disabled.Error.Code.Should().Be("gym.feature_disabled");
    }

    [Fact]
    public async Task CreateBranch_RejectsDuplicateCodeInsideTheTenant()
    {
        var branches = Substitute.For<IBranchRepository>();
        branches.CodeExistsAsync(_tenantId, "MAIN", Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreateBranchCommandHandler(Allow(), Actor(), branches, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

        var result = await handler.Handle(new CreateBranchCommand("Main", " main ", null, null, null, null, null, null, null, "UTC"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gym.conflict");
        await branches.DidNotReceive().AddAsync(Arg.Any<Branch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateBranch_UsesCurrentTenant()
    {
        var branches = Substitute.For<IBranchRepository>();
        branches.GetAsync(_tenantId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Branch?)null);
        var handler = new DeactivateBranchCommandHandler(Allow(), Actor(), branches, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

        var result = await handler.Handle(new DeactivateBranchCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be("gym.not_found");
        await branches.Received(1).GetAsync(_tenantId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckIn_RejectsMissingMemberFrozenMembershipOpenVisitAndForeignQr()
    {
        var people = Substitute.For<IPeopleRepository>();
        var attendance = Substitute.For<IAttendanceRepository>();
        var handler = CheckIn(people, attendance, Substitute.For<IBranchRepository>(), Substitute.For<IMembershipRepository>());
        people.GetMemberAsync(_tenantId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);

        var missing = await handler.Handle(new CheckInCommand(Guid.NewGuid(), Guid.NewGuid(), CheckInMethod.Reception, null), CancellationToken.None);
        missing.Error.Code.Should().Be("gym.not_found");

        var member = MemberProfile.Create(Guid.NewGuid(), _tenantId, _userId, "M12345678", DateOnly.FromDateTime(_now));
        var branch = Branch.Create(Guid.NewGuid(), _tenantId, "Main", "MAIN", "UTC");
        var frozen = Membership(member.Id, branch.Id, DateOnly.FromDateTime(_now).AddDays(-1), 30);
        frozen.Freeze(DateOnly.FromDateTime(_now));
        var frozenReady = Ready(member, branch, frozen);
        var frozenResult = await frozenReady.Handler.Handle(new CheckInCommand(member.Id, branch.Id, CheckInMethod.Reception, null), CancellationToken.None);
        frozenResult.Error.Code.Should().Be("gym.invalid_state");

        var open = AttendanceRecord.CheckIn(Guid.NewGuid(), _tenantId, branch.Id, member.Id, frozen.Id, CheckInMethod.Manual, _now);
        var active = Membership(member.Id, branch.Id, DateOnly.FromDateTime(_now), 30);
        var ready = Ready(member, branch, active);
        ready.Attendance.GetOpenAsync(_tenantId, member.Id, Arg.Any<CancellationToken>()).Returns(open);
        var duplicate = await ready.Handler.Handle(new CheckInCommand(member.Id, branch.Id, CheckInMethod.Reception, null), CancellationToken.None);
        duplicate.Error.Code.Should().Be("gym.conflict");

        var foreign = QrCheckInToken.Issue(Guid.NewGuid(), Guid.NewGuid(), member.Id, "hash", _now, _now.AddMinutes(10), _userId);
        ready.Attendance.GetTokenByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(foreign);
        ready.Tokens.Hash("raw").Returns("hash");
        var otherTenant = await ready.Handler.Handle(new CheckInCommand(null, branch.Id, CheckInMethod.QR, "raw"), CancellationToken.None);
        otherTenant.Error.Code.Should().Be("gym.invalid_state");
        await ready.Attendance.DidNotReceive().AddAsync(Arg.Any<AttendanceRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckIn_AcceptsActiveMembershipAndCheckoutClosesIt()
    {
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantId, _userId, "M12345678", DateOnly.FromDateTime(_now));
        var branch = Branch.Create(Guid.NewGuid(), _tenantId, "Main", "MAIN", "UTC");
        var membership = Membership(member.Id, branch.Id, DateOnly.FromDateTime(_now), 30);
        var ready = Ready(member, branch, membership);

        var result = await ready.Handler.Handle(new CheckInCommand(member.Id, branch.Id, CheckInMethod.Reception, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CheckInMethod.Should().Be(CheckInMethod.Reception);
        await ready.Attendance.Received(1).AddAsync(Arg.Any<AttendanceRecord>(), Arg.Any<CancellationToken>());

        var visit = AttendanceRecord.CheckIn(result.Value.Id, _tenantId, branch.Id, member.Id, membership.Id, CheckInMethod.Reception, _now);
        ready.Attendance.GetAsync(_tenantId, visit.Id, Arg.Any<CancellationToken>()).Returns(visit);
        ready.People.GetMemberAsync(_tenantId, member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_userId, _tenantId, PermissionCodes.MemberRead, Arg.Any<CancellationToken>()).Returns(true);
        var checkout = new CheckOutCommandHandler(Allow(), Actor(), permissions, ready.People, ready.Attendance, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());
        var closed = await checkout.Handle(new CheckOutCommand(visit.Id), CancellationToken.None);
        closed.IsSuccess.Should().BeTrue();
        closed.Value.CheckOutTime.Should().NotBeNull();
    }

    [Fact]
    public async Task ChangeMembership_RejectsCancelledRenewalAndRecordsFreeze()
    {
        var membership = Membership(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(_now), 30);
        membership.Cancel();
        var plans = Substitute.For<IMembershipRepository>();
        plans.GetMembershipAsync(_tenantId, membership.Id, Arg.Any<CancellationToken>()).Returns(membership);
        plans.GetPlanAsync(_tenantId, membership.MembershipPlanId, Arg.Any<CancellationToken>()).Returns(MembershipPlan.Create(membership.MembershipPlanId, _tenantId, "Monthly", null, 30, MembershipType.Monthly, null, 10m, "USD"));
        var handler = new ChangeMembershipCommandHandler(Allow(), Actor(), plans, Substitute.For<IBusinessConfigRepository>(), Substitute.For<IMembershipApprovalQueue>(), Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

        var renew = await handler.Handle(new ChangeMembershipCommand(membership.Id, "renew", null, false), CancellationToken.None);
        renew.Error.Code.Should().Be("gym.invalid_state");

        var active = Membership(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(_now), 30);
        plans.GetMembershipAsync(_tenantId, active.Id, Arg.Any<CancellationToken>()).Returns(active);
        var before = active.EndDate;
        var frozen = await handler.Handle(new ChangeMembershipCommand(active.Id, "freeze", "travel", false, 5), CancellationToken.None);
        frozen.IsSuccess.Should().BeTrue();
        frozen.Value.Status.Should().Be(GymMembershipStatus.Frozen);
        frozen.Value.EndDate.Should().Be(before.AddDays(5));
        await plans.Received(1).AddHistoryAsync(Arg.Is<MembershipHistoryEntry>(entry => entry.Action == MembershipAction.Frozen && entry.TenantId == _tenantId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void StaffValidator_RejectsOtherType()
    {
        var result = new AddStaffCommandValidator().Validate(new AddStaffCommand("a@b.co", "Correct-Horse-1", "Ada", "Lovelace", null, StaffType.Other, null));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Dashboard_QueriesOnlyTheCurrentTenant()
    {
        var dashboard = Substitute.For<IDashboardQuery>();
        var profiles = Substitute.For<IBusinessProfileRepository>();
        profiles.GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(BusinessProfile.Create(Guid.NewGuid(), _tenantId, "UTC", "USD"));
        dashboard.GetAsync(_tenantId, null, _now, TimeZoneInfo.Utc, Arg.Any<CancellationToken>()).Returns(new BusinessDashboard(1, 0, 1, 0, 0, 2, 4, 1, 1));
        var handler = new LifeFitAI.Application.Gym.Dashboard.GetBusinessDashboardQueryHandler(Allow(), Actor(), dashboard, profiles, Clock());

        var result = await handler.Handle(new LifeFitAI.Application.Gym.Dashboard.GetBusinessDashboardQuery(null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveMembers.Should().Be(1);
        result.Value.TodayCheckIns.Should().Be(2);
        await dashboard.Received(1).GetAsync(_tenantId, null, _now, TimeZoneInfo.Utc, Arg.Any<CancellationToken>());
    }

    private (IPeopleRepository People, IAttendanceRepository Attendance, ISecretTokenFactory Tokens, CheckInCommandHandler Handler) Ready(MemberProfile member, Branch branch, GymMembership membership)
    {
        var people = Substitute.For<IPeopleRepository>();
        var branches = Substitute.For<IBranchRepository>();
        var memberships = Substitute.For<IMembershipRepository>();
        var attendance = Substitute.For<IAttendanceRepository>();
        people.GetMemberAsync(_tenantId, member.Id, Arg.Any<CancellationToken>()).Returns(member);
        branches.GetAsync(_tenantId, branch.Id, Arg.Any<CancellationToken>()).Returns(branch);
        var page = new PageResult<GymMembershipResponse>([new GymMembershipResponse(membership.Id, member.Id, branch.Id, membership.MembershipPlanId, membership.MembershipNumber, membership.StartDate, membership.EndDate, membership.Status, membership.AutoRenew, membership.Notes)], 1, 20, 1);
        memberships.ListMembershipsAsync(_tenantId, GymMembershipStatus.Active, null, branch.Id, null, null, Arg.Any<PageQuery>(), Arg.Any<CancellationToken>()).Returns(page);
        memberships.GetMembershipAsync(_tenantId, membership.Id, Arg.Any<CancellationToken>()).Returns(membership);
        memberships.GetPlanAsync(_tenantId, membership.MembershipPlanId, Arg.Any<CancellationToken>()).Returns(MembershipPlan.Create(membership.MembershipPlanId, _tenantId, "Monthly", null, 30, MembershipType.Monthly, null, 10m, "USD"));
        var tokens = Substitute.For<ISecretTokenFactory>();
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_userId, _tenantId, PermissionCodes.MemberRead, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CheckInCommandHandler(Allow(), Actor(), permissions, people, branches, memberships, attendance, tokens, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());
        return (people, attendance, tokens, handler);
    }

    private CheckInCommandHandler CheckIn(IPeopleRepository people, IAttendanceRepository attendance, IBranchRepository branches, IMembershipRepository memberships) =>
        new(Allow(), Actor(), Substitute.For<IPermissionService>(), people, branches, memberships, attendance, Substitute.For<ISecretTokenFactory>(), Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

    private IOperationGuard Allow()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_userId);
        actor.TenantId.Returns(_tenantId);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_now);
        return clock;
    }

    private GymMembership Membership(Guid memberId, Guid branchId, DateOnly start, int days) =>
        GymMembership.Create(Guid.NewGuid(), _tenantId, memberId, branchId, Guid.NewGuid(), "G12345678", start, days, DateOnly.FromDateTime(_now), false, null);
}
