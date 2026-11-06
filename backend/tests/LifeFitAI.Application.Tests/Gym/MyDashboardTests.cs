using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class MyDashboardTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Owner_KeepsTheExistingDashboard()
    {
        var handler = Handler(RoleNames.GymOwner, [PermissionCodes.ReportRead], out var reader, out _);
        var result = await handler.Handle(new GetMyDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Layout.Should().Be(MyDashboardCatalog.OwnerLayout);
        result.Value.Widgets.Should().BeEmpty();
        await reader.DidNotReceive().ReadAsync(Arg.Any<DashboardActorContext>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), Arg.Any<TimeZoneInfo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receptionist_SeesOwnCollectionsAndNotPayroll()
    {
        var permissions = new[]
        {
            PermissionCodes.DashboardWidgetCheckIns,
            PermissionCodes.DashboardWidgetCollections,
            PermissionCodes.DashboardWidgetPayroll,
            PermissionCodes.AttendanceRead,
            PermissionCodes.PaymentRead,
        };
        var handler = Handler(RoleNames.Receptionist, permissions, out var reader, out _);
        var result = await handler.Handle(new GetMyDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Layout.Should().Be(MyDashboardCatalog.ReceptionistLayout);
        result.Value.Widgets.Select(widget => widget.Code).Should().Equal("checkins", "collections");
        await reader.Received(1).ReadAsync(
            Arg.Is<DashboardActorContext>(context => context.TenantId == _tenantId && context.UserId == _userId && !context.PaymentsAll),
            false,
            false,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Is<IReadOnlyCollection<string>>(codes => codes.Contains("collections") && !codes.Contains("payroll")),
            _now,
            TimeZoneInfo.Utc,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receptionist_WithoutWidgetGrants_UsesTheDefaultLayout()
    {
        var permissions = new[]
        {
            PermissionCodes.MemberRead,
            PermissionCodes.MembershipRead,
            PermissionCodes.CrmRead,
            PermissionCodes.AttendanceRead,
            PermissionCodes.PaymentRead,
            PermissionCodes.MemberCreate,
        };
        var handler = Handler(RoleNames.Receptionist, permissions, out _, out _);
        var result = await handler.Handle(new GetMyDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Widgets.Select(widget => widget.Code).Should().Equal("checkins", "new-members", "expiring", "collections", "follow-ups", "operations");
        result.Value.Actions.Select(action => action.Label).Should().Equal("Add a member");
    }

    [Fact]
    public async Task Receptionist_DefaultLayout_KeepsWidgetsWhenFeatureFlagsAreOff()
    {
        var permissions = new[] { PermissionCodes.MemberRead, PermissionCodes.MembershipRead, PermissionCodes.CrmRead };
        var handler = Handler(RoleNames.Receptionist, permissions, out _, out var features);
        features.IsEnabledAsync(_tenantId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await handler.Handle(new GetMyDashboardQuery(), CancellationToken.None);

        result.Value.Widgets.Select(widget => widget.Code).Should().Equal("new-members", "expiring", "follow-ups");
    }

    [Fact]
    public async Task Trainer_IsLimitedToAssignedCoachData()
    {
        var permissions = new[]
        {
            PermissionCodes.DashboardWidgetSessions,
            PermissionCodes.DashboardWidgetAssignedMembers,
            PermissionCodes.WorkoutRead,
            PermissionCodes.MemberRead,
        };
        var handler = Handler(RoleNames.Trainer, permissions, out var reader, out _);
        var result = await handler.Handle(new GetMyDashboardQuery(), CancellationToken.None);

        result.Value.Layout.Should().Be(MyDashboardCatalog.TrainerLayout);
        result.Value.Widgets.Select(widget => widget.Code).Should().Equal("sessions", "assigned-members");
        await reader.Received(1).DescribeAsync(_tenantId, _userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accountant_HidesPayrollWhenTheModuleIsOff()
    {
        var permissions = new[]
        {
            PermissionCodes.DashboardWidgetPayroll,
            PermissionCodes.DashboardWidgetFinancials,
            PermissionCodes.PayrollRead,
            PermissionCodes.LedgerRead,
        };
        var handler = Handler(RoleNames.Accountant, permissions, out _, out var features);
        features.IsEnabledAsync(_tenantId, FeatureCodes.GymStaffPayroll, Arg.Any<CancellationToken>()).Returns(false);

        var result = await handler.Handle(new GetMyDashboardQuery(), CancellationToken.None);

        result.Value.Widgets.Select(widget => widget.Code).Should().Equal("financials");
    }

    [Fact]
    public async Task Manager_LimitsBranchDataWhenTheScopeIsNotAll()
    {
        var permissions = new[]
        {
            PermissionCodes.DashboardWidgetAttendance,
            PermissionCodes.DashboardWidgetFollowUps,
            PermissionCodes.AttendanceRead,
            PermissionCodes.CrmRead,
        };
        var handler = Handler(RoleNames.GymManager, permissions, out var reader, out _, AccessScopes.Assigned);
        var result = await handler.Handle(new GetMyDashboardQuery(), CancellationToken.None);

        result.Value.Layout.Should().Be(MyDashboardCatalog.ManagerLayout);
        await reader.Received(1).ReadAsync(
            Arg.Is<DashboardActorContext>(context => context.TenantId == _tenantId && context.BranchId != null),
            true,
            true,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<DateTime>(),
            Arg.Any<TimeZoneInfo>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PermissionChange_ChangesTheWidgetsOnTheNextRead()
    {
        var first = new HashSet<string>([PermissionCodes.DashboardWidgetSchedule], StringComparer.Ordinal);
        var second = new HashSet<string>([PermissionCodes.DashboardWidgetTasks, PermissionCodes.DashboardWidgetChats, PermissionCodes.ChatRead], StringComparer.Ordinal);
        var enabled = new HashSet<string>([FeatureCodes.GymChat], StringComparer.Ordinal);

        MyDashboardCatalog.Visible(MyDashboardCatalog.StaffLayout, first, enabled).Select(widget => widget.Code).Should().Equal("schedule");
        MyDashboardCatalog.Visible(MyDashboardCatalog.StaffLayout, second, enabled).Select(widget => widget.Code).Should().Equal("tasks", "chats");
    }

    [Fact]
    public async Task MissingMembership_RejectsTheDashboard()
    {
        var access = Substitute.For<IDashboardAccess>();
        access.OpenMemberAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure<DashboardWorkspace>(IdentityErrors.NotMember));
        var reader = Substitute.For<IMyDashboardReader>();
        var handler = new GetMyDashboardQueryHandler(
            access,
            Substitute.For<IUserAccountRepository>(),
            Substitute.For<IRoleRepository>(),
            Substitute.For<IFeatureFlagService>(),
            Substitute.For<ISubscriptionService>(),
            Substitute.For<IMemberAccessScope>(),
            reader,
            Clock());

        var result = await handler.Handle(new GetMyDashboardQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("tenant.not_member");
        await reader.DidNotReceive().DescribeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SharedPayments_RequireAnExplicitAllScope()
    {
        MyDashboardCatalog.PaymentsAreShared([AccessScopes.Own]).Should().BeFalse();
        MyDashboardCatalog.PaymentsAreShared([AccessScopes.All]).Should().BeTrue();
    }

    private GetMyDashboardQueryHandler Handler(
        string role,
        IReadOnlyList<string> permissions,
        out IMyDashboardReader reader,
        out IFeatureFlagService features,
        string memberScope = AccessScopes.All)
    {
        var access = Substitute.For<IDashboardAccess>();
        var tenant = Tenant.Create(_tenantId, "Pro ultimate", TenantType.Gym);
        access.OpenMemberAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(new DashboardWorkspace(tenant, null, TimeZoneInfo.Utc, _now, permissions, _userId)));
        var users = Substitute.For<IUserAccountRepository>();
        users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(UserAccount.Create(_userId, _tenantId, "staff2@gym.test", null, "hash", "salt", "Staff", "Two"));
        var roles = Substitute.For<IRoleRepository>();
        roles.GetRoleNamesAsync(_userId, _tenantId, Arg.Any<CancellationToken>()).Returns(new[] { role });
        features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenantId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var subscriptions = Substitute.For<ISubscriptionService>();
        subscriptions.IsUsableAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(true);
        var scope = Substitute.For<IMemberAccessScope>();
        scope.LimitAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns(new MemberAccessLimit(memberScope, null, null, false));
        reader = Substitute.For<IMyDashboardReader>();
        var branchId = Guid.NewGuid();
        reader.DescribeAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(new DashboardActorContext(_tenantId, _userId, Guid.NewGuid(), branchId, false, "USD"));
        reader.ReadAsync(Arg.Any<DashboardActorContext>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), Arg.Any<TimeZoneInfo>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyCollection<string>>(4).ToDictionary(code => code, _ => new DashboardMetric("1", "Counted"), StringComparer.Ordinal));
        return new GetMyDashboardQueryHandler(access, users, roles, features, subscriptions, scope, reader, Clock());
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_now);
        return clock;
    }
}
