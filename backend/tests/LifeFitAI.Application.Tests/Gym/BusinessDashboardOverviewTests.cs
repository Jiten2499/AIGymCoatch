using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class BusinessDashboardOverviewTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 10, 5, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Overview_PlatformTenant_RejectsAccess()
    {
        var handler = Handler(out _, out var features);
        Tenants().GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(Tenant.Create(_tenantId, "LifeFit AI", TenantType.Platform));

        var result = await handler.Handle(new GetBusinessDashboardOverviewQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("tenant.not_business");
        await features.DidNotReceive().IsEnabledAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Overview_MissingMembership_RejectsAccess()
    {
        var handler = Handler(out var members, out _);
        Tenants().GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(Tenant.Create(_tenantId, "North Athletic", TenantType.Gym));
        members.GetActiveAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((TenantMember?)null);

        var result = await handler.Handle(new GetBusinessDashboardOverviewQuery(), CancellationToken.None);

        result.Error.Code.Should().Be("tenant.not_member");
    }

    [Fact]
    public async Task Overview_SuspendedTenant_RejectsAccess()
    {
        var tenant = Tenant.Create(_tenantId, "North Athletic", TenantType.Gym);
        tenant.Suspend();
        var handler = Handler(out var members, out _);
        Tenants().GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await handler.Handle(new GetBusinessDashboardOverviewQuery(), CancellationToken.None);

        result.Error.Code.Should().Be("tenant.unavailable");
        await members.DidNotReceive().GetActiveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Overview_AuthorizedOwner_ReturnsEnabledMetricsOnly()
    {
        var handler = Ready(out var features);
        features.IsEnabledAsync(_tenantId, FeatureCodes.MemberManagement, Arg.Any<CancellationToken>()).Returns(true);

        var result = await handler.Handle(new GetBusinessDashboardOverviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tenant.Name.Should().Be("North Athletic");
        result.Value.Tenant.LogoUrl.Should().Be("https://cdn.example/logo.png");
        result.Value.Tenant.TimeZone.Should().Be("UTC");
        result.Value.User.DisplayName.Should().Be("Ada Lovelace");
        result.Value.User.Roles.Should().Contain(RoleNames.GymOwner);
        result.Value.Summary.Members.Should().NotBeNull();
        result.Value.Summary.Members!.Active.Should().Be(4);
        result.Value.Summary.Attendance.Should().BeNull();
        result.Value.Summary.Memberships.Should().BeNull();
        result.Value.Summary.People.Should().BeNull();
        result.Value.Capabilities.Should().Contain(item => item.Code == "Members" && item.Enabled);
        await features.DidNotReceive().IsEnabledAsync(_tenantId, FeatureCodes.Attendance, Arg.Any<CancellationToken>());
        await Kpis().Received(1).CountAsync(_tenantId, _now, TimeZoneInfo.Utc, Arg.Is<KpiScope>(scope => scope.Members && !scope.Attendance), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Overview_DisabledFeature_HidesCapability()
    {
        var handler = Ready(out var features);
        features.IsEnabledAsync(_tenantId, FeatureCodes.MemberManagement, Arg.Any<CancellationToken>()).Returns(true);
        features.IsEnabledAsync(_tenantId, FeatureCodes.BusinessReports, Arg.Any<CancellationToken>()).Returns(false);

        var result = await handler.Handle(new GetBusinessDashboardOverviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Capabilities.Should().Contain(item => item.Code == "Members" && item.Enabled);
        result.Value.Capabilities.Should().Contain(item => item.Code == "Reports" && !item.Enabled);
    }

    [Fact]
    public async Task Overview_NoEnabledKpi_SkipsTheCountQuery()
    {
        var handler = Ready(out _);

        var result = await handler.Handle(new GetBusinessDashboardOverviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.Members.Should().BeNull();
        result.Value.Summary.Attendance.Should().BeNull();
        result.Value.Summary.Memberships.Should().BeNull();
        result.Value.Summary.People.Should().BeNull();
        await Kpis().DidNotReceive().CountAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeZoneInfo>(), Arg.Any<KpiScope>(), Arg.Any<CancellationToken>());
    }

    private GetBusinessDashboardOverviewQueryHandler Ready(out IFeatureFlagService features)
    {
        var handler = Handler(out var members, out features);
        var profile = BusinessProfile.Create(Guid.NewGuid(), _tenantId, "UTC", "USD");
        profile.Update(null, null, null, null, null, null, null, null, null, null, "https://cdn.example/logo.png", "UTC", "USD");
        Tenants().GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(Tenant.Create(_tenantId, "North Athletic", TenantType.Gym));
        members.GetActiveAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns(TenantMember.Create(Guid.NewGuid(), _tenantId, _userId, _now));
        Users().GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(UserAccount.Create(_userId, _tenantId, "ada@north.test", null, "hash", "salt", "Ada", "Lovelace"));
        Profiles().GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(profile);
        Roles().GetPermissionCodesAsync(_userId, _tenantId, Arg.Any<CancellationToken>()).Returns(new[] { PermissionCodes.ReportRead, PermissionCodes.MemberRead });
        Roles().GetRoleNamesAsync(_userId, _tenantId, Arg.Any<CancellationToken>()).Returns(new[] { RoleNames.GymOwner });
        return handler;
    }

    private readonly ITenantRepository _tenants = Substitute.For<ITenantRepository>();
    private readonly IUserAccountRepository _users = Substitute.For<IUserAccountRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IBusinessProfileRepository _profiles = Substitute.For<IBusinessProfileRepository>();
    private readonly IDashboardQuery _kpis = Substitute.For<IDashboardQuery>();

    private ITenantRepository Tenants() => _tenants;

    private IUserAccountRepository Users() => _users;

    private IRoleRepository Roles() => _roles;

    private IBusinessProfileRepository Profiles() => _profiles;

    private IDashboardQuery Kpis() => _kpis;

    private GetBusinessDashboardOverviewQueryHandler Handler(out ITenantMembershipRepository members, out IFeatureFlagService features)
    {
        members = Substitute.For<ITenantMembershipRepository>();
        features = Substitute.For<IFeatureFlagService>();
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_userId);
        actor.TenantId.Returns(_tenantId);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_now);
        _kpis.CountAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeZoneInfo>(), Arg.Any<KpiScope>(), Arg.Any<CancellationToken>())
            .Returns(new BusinessKpiCounts(5, 4, 1, 0, 0, 0, 0, 0, 0, 0));
        var access = new DashboardAccess(actor, _tenants, members, _roles, _profiles, features, clock);
        return new GetBusinessDashboardOverviewQueryHandler(access, _users, _roles, features, _kpis, clock);
    }
}
