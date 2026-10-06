using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class BusinessDashboardAnalyticsTests
{
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    [Fact]
    public void Calendar_RejectsAnInvertedOrOversizedRange()
    {
        ResolvedAnalytics.Resolve(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1), AnalyticsPeriod.Day, TimeZoneInfo.Utc).IsFailure.Should().BeTrue();
        ResolvedAnalytics.Resolve(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 3), AnalyticsPeriod.Day, TimeZoneInfo.Utc).IsFailure.Should().BeTrue();
        ResolvedAnalytics.Resolve(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 2), AnalyticsPeriod.Day, TimeZoneInfo.Utc).IsSuccess.Should().BeTrue();
        ResolvedAnalytics.Resolve(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5), (AnalyticsPeriod)0, TimeZoneInfo.Utc).Error.Code.Should().Be("gym.analytics_range");
    }

    [Fact]
    public void Calendar_BuildsContinuousWeeksAndZeroFillsMissingDays()
    {
        var calendar = ResolvedAnalytics.Resolve(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), AnalyticsPeriod.Week, TimeZoneInfo.Utc).Value;
        calendar.Buckets.Should().HaveCount(2);
        calendar.Buckets[0].Start.Should().Be(new DateOnly(2026, 10, 1));
        calendar.Buckets[0].End.Should().Be(new DateOnly(2026, 10, 4));
        calendar.Buckets[1].Start.Should().Be(new DateOnly(2026, 10, 5));

        var series = calendar.Series("newMembers", "New members", calendar.Rollup(
        [
            new DateCount(new DateOnly(2026, 10, 1), 2),
            new DateCount(new DateOnly(2026, 10, 3), 1),
        ]));
        series.Points.Select(point => point.Value).Should().Equal(3, 0);
    }

    [Fact]
    public void Calendar_UsesTenantLocalMidnightForAttendanceBounds()
    {
        var calendar = ResolvedAnalytics.Resolve(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5), AnalyticsPeriod.Day, India).Value;
        calendar.FromUtc.Should().Be(new DateTime(2026, 10, 4, 18, 30, 0, DateTimeKind.Utc));
        calendar.ToUtc.Should().Be(new DateTime(2026, 10, 5, 18, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Members_GroupsJoinsByTenantAndIgnoresAnotherGym()
    {
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using var db = CreateDb();
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "A", new DateOnly(2026, 10, 2)));
        var inactive = MemberProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "B", new DateOnly(2026, 10, 2));
        inactive.Update(null, null, null, null, null, null, null, null, ProfileStatus.Inactive);
        db.MemberProfiles.Add(inactive);
        var removed = MemberProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "C", new DateOnly(2026, 10, 2));
        removed.Deactivate(DateTime.UtcNow);
        db.MemberProfiles.Add(removed);
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), other, Guid.NewGuid(), "D", new DateOnly(2026, 10, 2)));
        await db.SaveChangesAsync();

        var counts = await new DashboardAnalyticsQuery(db).CountMemberJoinsAsync(tenant, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), CancellationToken.None);
        var calendar = ResolvedAnalytics.Resolve(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), AnalyticsPeriod.Day, TimeZoneInfo.Utc).Value;
        calendar.Series("newMembers", "New members", calendar.Rollup(counts)).Points.Select(point => point.Value).Should().Equal(0, 2, 0);
        var empty = await new DashboardAnalyticsQuery(db).CountMemberJoinsAsync(Guid.NewGuid(), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), CancellationToken.None);
        empty.Should().BeEmpty();
    }

    [Fact]
    public async Task Attendance_UsesTheTenantLocalDayAndExcludesOtherGyms()
    {
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using var db = CreateDb();
        db.Attendance.Add(Visit(tenant, new DateTime(2026, 10, 4, 18, 29, 0, DateTimeKind.Utc)));
        db.Attendance.Add(Visit(tenant, new DateTime(2026, 10, 4, 18, 30, 0, DateTimeKind.Utc)));
        db.Attendance.Add(Visit(other, new DateTime(2026, 10, 4, 18, 30, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var calendar = ResolvedAnalytics.Resolve(new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 5), AnalyticsPeriod.Day, India).Value;
        var counts = await new DashboardAnalyticsQuery(db).CountCheckInsByLocalDateAsync(tenant, calendar.FromUtc, calendar.ToUtc, India, CancellationToken.None);
        calendar.Series("checkIns", "Check-ins", calendar.Rollup(counts)).Points.Select(point => point.Value).Should().Equal(1, 1);
    }

    [Fact]
    public async Task Attendance_SplitsAQuarterThatCrossesLocalMidnight()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Offset20", TimeSpan.FromMinutes(20), "Offset20", "Offset20");
        var tenant = Guid.NewGuid();
        await using var db = CreateDb();
        db.Attendance.Add(Visit(tenant, new DateTime(2026, 10, 4, 23, 39, 0, DateTimeKind.Utc)));
        db.Attendance.Add(Visit(tenant, new DateTime(2026, 10, 4, 23, 40, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var calendar = ResolvedAnalytics.Resolve(new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 5), AnalyticsPeriod.Day, zone).Value;
        var counts = await new DashboardAnalyticsQuery(db).CountCheckInsByLocalDateAsync(tenant, calendar.FromUtc, calendar.ToUtc, zone, CancellationToken.None);
        calendar.Series("checkIns", "Check-ins", calendar.Rollup(counts)).Points.Select(point => point.Value).Should().Equal(1, 1);
    }

    [Fact]
    public async Task Memberships_CountsCreatedRenewedAndExpiredHistoryOnly()
    {
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        var day = new DateOnly(2026, 10, 2);
        await using var db = CreateDb();
        db.MembershipHistory.Add(History(tenant, MembershipAction.Created, day));
        db.MembershipHistory.Add(History(tenant, MembershipAction.Renewed, day));
        db.MembershipHistory.Add(History(tenant, MembershipAction.Expired, day));
        db.MembershipHistory.Add(History(tenant, MembershipAction.Frozen, day));
        db.MembershipHistory.Add(History(other, MembershipAction.Created, day));
        await db.SaveChangesAsync();

        var counts = await new DashboardAnalyticsQuery(db).CountMembershipActivityAsync(tenant, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), CancellationToken.None);
        counts.Should().BeEquivalentTo(
        [
            new MembershipActivityCount(day, "Created", 1),
            new MembershipActivityCount(day, "Renewed", 1),
            new MembershipActivityCount(day, "Expired", 1),
        ]);
    }

    [Fact]
    public async Task Analytics_UnauthorizedAndOutOfRangeRequestsDoNotQuery()
    {
        var analytics = Substitute.For<IDashboardAnalytics>();
        var denied = Handler(analytics, out var features, TenantType.Platform, TenantStatus.Active, reportRead: true, memberRead: true);
        var platform = await denied.Handle(new GetMemberGrowthAnalyticsQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), AnalyticsPeriod.Day), CancellationToken.None);
        platform.Error.Code.Should().Be("tenant.not_business");

        var suspendedTenant = Tenant.Create(TenantId, "North", TenantType.Gym);
        suspendedTenant.Suspend();
        var suspended = Handler(analytics, out _, TenantType.Gym, TenantStatus.Active, reportRead: true, memberRead: true, tenant: suspendedTenant);
        (await suspended.Handle(new GetMemberGrowthAnalyticsQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), AnalyticsPeriod.Day), CancellationToken.None)).Error.Code.Should().Be("tenant.unavailable");

        var missingArea = Handler(analytics, out features, TenantType.Gym, TenantStatus.Active, reportRead: true, memberRead: false);
        (await missingArea.Handle(new GetMemberGrowthAnalyticsQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), AnalyticsPeriod.Day), CancellationToken.None)).Error.Code.Should().Be("authorization.forbidden");

        var disabled = Handler(analytics, out features, TenantType.Gym, TenantStatus.Active, reportRead: true, memberRead: true);
        (await disabled.Handle(new GetMemberGrowthAnalyticsQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), AnalyticsPeriod.Day), CancellationToken.None)).Error.Code.Should().Be("gym.feature_disabled");

        var oversized = Handler(analytics, out features, TenantType.Gym, TenantStatus.Active, reportRead: true, memberRead: true);
        features.IsEnabledAsync(TenantId, FeatureCodes.MemberManagement, Arg.Any<CancellationToken>()).Returns(true);
        (await oversized.Handle(new GetMemberGrowthAnalyticsQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), AnalyticsPeriod.Day), CancellationToken.None)).Error.Code.Should().Be("gym.analytics_range");
        await analytics.DidNotReceive().CountMemberJoinsAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Analytics_AuthorizedUsersShareTheTenantSeries()
    {
        var analytics = Substitute.For<IDashboardAnalytics>();
        analytics.CountMemberJoinsAsync(TenantId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DateCount(new DateOnly(2026, 10, 2), 4) });
        var first = Handler(analytics, out var features, TenantType.Gym, TenantStatus.Active, reportRead: true, memberRead: true);
        features.IsEnabledAsync(TenantId, FeatureCodes.MemberManagement, Arg.Any<CancellationToken>()).Returns(true);
        var secondUser = Guid.NewGuid();
        var second = Handler(analytics, out var secondFeatures, TenantType.Gym, TenantStatus.Active, reportRead: true, memberRead: true, userId: secondUser);
        secondFeatures.IsEnabledAsync(TenantId, FeatureCodes.MemberManagement, Arg.Any<CancellationToken>()).Returns(true);

        var left = await first.Handle(new GetMemberGrowthAnalyticsQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), AnalyticsPeriod.Day), CancellationToken.None);
        var right = await second.Handle(new GetMemberGrowthAnalyticsQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), AnalyticsPeriod.Day), CancellationToken.None);

        left.Value.Series[0].Points.Select(point => point.Value).Should().Equal(0, 4, 0);
        right.Value.Series[0].Points.Select(point => point.Value).Should().Equal(left.Value.Series[0].Points.Select(point => point.Value));
        await analytics.Received(2).CountMemberJoinsAsync(TenantId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), Arg.Any<CancellationToken>());
    }

    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static GetMemberGrowthAnalyticsQueryHandler Handler(
        IDashboardAnalytics analytics,
        out IFeatureFlagService features,
        TenantType type,
        TenantStatus status,
        bool reportRead,
        bool memberRead,
        Tenant? tenant = null,
        Guid? userId = null)
    {
        var currentUser = userId ?? Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        features = Substitute.For<IFeatureFlagService>();
        var tenants = Substitute.For<ITenantRepository>();
        var members = Substitute.For<ITenantMembershipRepository>();
        var roles = Substitute.For<IRoleRepository>();
        var profiles = Substitute.For<IBusinessProfileRepository>();
        var actor = Substitute.For<ICurrentActor>();
        var clock = Substitute.For<IDateTimeProvider>();
        actor.UserId.Returns(currentUser);
        actor.TenantId.Returns(TenantId);
        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        clock.UtcNow.Returns(now);
        tenant ??= Tenant.Create(TenantId, "North", type);
        if (status == TenantStatus.Suspended && tenant.Status != TenantStatus.Suspended)
        {
            tenant.Suspend();
        }

        var member = TenantMember.Create(Guid.NewGuid(), TenantId, currentUser, now);
        tenants.GetAsync(TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
        members.GetActiveAsync(TenantId, currentUser, Arg.Any<CancellationToken>()).Returns(member);
        var permissions = new List<string>();
        if (reportRead)
        {
            permissions.Add(PermissionCodes.ReportRead);
        }

        if (memberRead)
        {
            permissions.Add(PermissionCodes.MemberRead);
        }

        roles.GetPermissionCodesAsync(currentUser, TenantId, Arg.Any<CancellationToken>()).Returns(permissions);
        profiles.GetAsync(TenantId, Arg.Any<CancellationToken>()).Returns(BusinessProfile.Create(Guid.NewGuid(), TenantId, "UTC", "USD"));
        var access = new DashboardAccess(actor, tenants, members, roles, profiles, features, clock);
        return new GetMemberGrowthAnalyticsQueryHandler(access, analytics);
    }

    private static MembershipHistoryEntry History(Guid tenantId, MembershipAction action, DateOnly day) =>
        MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, Guid.NewGuid(), action, null, GymMembershipStatus.Active, day, null, null, DateTime.UtcNow);

    private static AttendanceRecord Visit(Guid tenantId, DateTime checkIn) =>
        AttendanceRecord.CheckIn(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CheckInMethod.Manual, checkIn);

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new RowVersionInterceptor())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class RowVersionInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries())
            {
                var property = entry.Properties.FirstOrDefault(item => item.Metadata.Name == "RowVersion");
                if (property is not null && property.CurrentValue is null)
                {
                    property.CurrentValue = new byte[] { 1 };
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
