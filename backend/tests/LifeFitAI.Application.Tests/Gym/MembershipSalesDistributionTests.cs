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

public sealed class MembershipSalesDistributionTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Share_UsesSoldCountsAndKeepsPercentagesAtOneHundred()
    {
        var monthly = Guid.NewGuid();
        var annual = Guid.NewGuid();
        var slices = MembershipSalesShare.Calculate(
        [
            new MembershipSaleCount(annual, "Annual", 1),
            new MembershipSaleCount(monthly, "Monthly", 2),
            new MembershipSaleCount(Guid.NewGuid(), "Unused", 0),
        ]);

        slices.Select(slice => (slice.PackageName, slice.Sold, slice.Percentage)).Should().Equal(("Monthly", 2, 66.7m), ("Annual", 1, 33.3m));
        slices.Sum(slice => slice.Percentage).Should().Be(100m);

        var tied = MembershipSalesShare.Calculate(
        [
            new MembershipSaleCount(Guid.NewGuid(), "C", 1),
            new MembershipSaleCount(Guid.NewGuid(), "A", 1),
            new MembershipSaleCount(Guid.NewGuid(), "B", 1),
        ]);
        tied.Select(slice => slice.Percentage).Should().Equal(33.4m, 33.3m, 33.3m);
        tied.Sum(slice => slice.Percentage).Should().Be(100m);
        MembershipSalesShare.Calculate([]).Should().BeEmpty();
    }

    [Fact]
    public async Task Sales_GroupsEligibleAssignmentsByPackageAndTenant()
    {
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        var day = new DateOnly(2026, 10, 2);
        var today = new DateOnly(2026, 10, 5);
        await using var db = CreateDb();
        var monthly = Plan(tenant, "Monthly");
        var annual = Plan(tenant, "Annual");
        var foreign = Plan(other, "Foreign");
        db.MembershipPlans.AddRange(monthly, annual, foreign);
        var member = MemberProfile.Create(Guid.NewGuid(), tenant, null, "M1", day);
        db.MemberProfiles.Add(member);

        var first = Sale(db, tenant, member.Id, monthly.Id, day, today, MembershipAction.Created);
        Sale(db, tenant, member.Id, monthly.Id, day, today, MembershipAction.Created);
        Sale(db, tenant, member.Id, annual.Id, day, today, MembershipAction.Renewed);
        var expired = Sale(db, tenant, member.Id, monthly.Id, day, today, MembershipAction.Created, durationDays: 1);
        expired.Expire(today);
        var cancelled = Sale(db, tenant, member.Id, monthly.Id, day, today, MembershipAction.Created);
        cancelled.Cancel();
        var voided = Sale(db, tenant, member.Id, annual.Id, day, today, MembershipAction.Created);
        var payment = MembershipPayment.Create(Guid.NewGuid(), tenant, member.Id, voided.Id, 25m, "USD", MembershipPaymentMethod.Cash, day, Guid.NewGuid(), null, null);
        payment.Void(Guid.NewGuid(), DateTime.UtcNow);
        db.MembershipPayments.Add(payment);
        var removed = Sale(db, tenant, member.Id, monthly.Id, day, today, MembershipAction.Created);
        removed.SoftDelete(DateTime.UtcNow);
        Sale(db, tenant, member.Id, monthly.Id, day.AddDays(2), today, MembershipAction.Created);
        db.MembershipHistory.Add(MembershipHistoryEntry.Create(Guid.NewGuid(), tenant, first.Id, MembershipAction.Frozen, GymMembershipStatus.Active, GymMembershipStatus.Frozen, day, null, null, DateTime.UtcNow));
        Sale(db, other, MemberProfile.Create(Guid.NewGuid(), other, null, "M2", day).Id, foreign.Id, day, today, MembershipAction.Created);
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), other, null, "M2", day));
        await db.SaveChangesAsync();

        var counts = await new DashboardAnalyticsQuery(db).CountMembershipSalesAsync(tenant, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), CancellationToken.None);
        var slices = MembershipSalesShare.Calculate(counts);

        slices.Should().Equal(
            new MembershipSalesSlice(monthly.Id, "Monthly", 3, 75.0m),
            new MembershipSalesSlice(annual.Id, "Annual", 1, 25.0m));
        slices.Sum(slice => slice.Sold).Should().Be(4);
        (await new DashboardAnalyticsQuery(db).CountMembershipSalesAsync(Guid.NewGuid(), day, day, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Sales_UsesTheTenantWindowAndRejectsUnauthorizedCallers()
    {
        var analytics = Substitute.For<IDashboardAnalytics>();
        var planId = Guid.NewGuid();
        analytics.CountMembershipSalesAsync(TenantId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), Arg.Any<CancellationToken>())
            .Returns(new[] { new MembershipSaleCount(planId, "Monthly", 4) });
        var handler = Handler(analytics, out var features, membershipRead: true, featureEnabled: true);
        features.IsEnabledAsync(TenantId, FeatureCodes.MembershipManagement, Arg.Any<CancellationToken>()).Returns(true);

        var result = await handler.Handle(new GetMembershipSalesDistributionQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), AnalyticsPeriod.Day), CancellationToken.None);

        result.Value.TotalSold.Should().Be(4);
        result.Value.Slices.Single().Percentage.Should().Be(100m);
        await analytics.Received(1).CountMembershipSalesAsync(TenantId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), Arg.Any<CancellationToken>());

        var denied = Handler(Substitute.For<IDashboardAnalytics>(), out _, membershipRead: false, featureEnabled: true);
        (await denied.Handle(new GetMembershipSalesDistributionQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), AnalyticsPeriod.Day), CancellationToken.None)).Error.Code.Should().Be("authorization.forbidden");

        var disabled = Handler(Substitute.For<IDashboardAnalytics>(), out var disabledFeatures, membershipRead: true, featureEnabled: false);
        disabledFeatures.IsEnabledAsync(TenantId, FeatureCodes.MembershipManagement, Arg.Any<CancellationToken>()).Returns(false);
        (await disabled.Handle(new GetMembershipSalesDistributionQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), AnalyticsPeriod.Day), CancellationToken.None)).Error.Code.Should().Be("gym.feature_disabled");

        var oversized = Handler(analytics, out var oversizedFeatures, membershipRead: true, featureEnabled: true);
        oversizedFeatures.IsEnabledAsync(TenantId, FeatureCodes.MembershipManagement, Arg.Any<CancellationToken>()).Returns(true);
        (await oversized.Handle(new GetMembershipSalesDistributionQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), AnalyticsPeriod.Day), CancellationToken.None)).Error.Code.Should().Be("gym.analytics_range");
        await analytics.DidNotReceive().CountMembershipSalesAsync(Arg.Any<Guid>(), new DateOnly(2026, 1, 1), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    private static MembershipPlan Plan(Guid tenantId, string name) =>
        MembershipPlan.Create(Guid.NewGuid(), tenantId, name, null, 30, MembershipType.Monthly, null, 100m, "USD");

    private static GymMembership Sale(ApplicationDbContext db, Guid tenantId, Guid memberId, Guid planId, DateOnly day, DateOnly today, MembershipAction action, int durationDays = 30)
    {
        var membership = GymMembership.Create(Guid.NewGuid(), tenantId, memberId, null, planId, $"G{Guid.NewGuid():N}"[..8], day, durationDays, today, false, null);
        db.GymMemberships.Add(membership);
        db.MembershipHistory.Add(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, membership.Id, action, null, membership.Status, day, null, null, DateTime.UtcNow));
        return membership;
    }

    private static GetMembershipSalesDistributionQueryHandler Handler(IDashboardAnalytics analytics, out IFeatureFlagService features, bool membershipRead, bool featureEnabled)
    {
        features = Substitute.For<IFeatureFlagService>();
        var tenants = Substitute.For<ITenantRepository>();
        var members = Substitute.For<ITenantMembershipRepository>();
        var roles = Substitute.For<IRoleRepository>();
        var profiles = Substitute.For<IBusinessProfileRepository>();
        var actor = Substitute.For<ICurrentActor>();
        var clock = Substitute.For<IDateTimeProvider>();
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        actor.UserId.Returns(userId);
        actor.TenantId.Returns(TenantId);
        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        clock.UtcNow.Returns(now);
        tenants.GetAsync(TenantId, Arg.Any<CancellationToken>()).Returns(Tenant.Create(TenantId, "North", TenantType.Gym));
        members.GetActiveAsync(TenantId, userId, Arg.Any<CancellationToken>()).Returns(TenantMember.Create(Guid.NewGuid(), TenantId, userId, now));
        var permissions = new List<string> { PermissionCodes.ReportRead };
        if (membershipRead)
        {
            permissions.Add(PermissionCodes.MembershipRead);
        }

        roles.GetPermissionCodesAsync(userId, TenantId, Arg.Any<CancellationToken>()).Returns(permissions);
        profiles.GetAsync(TenantId, Arg.Any<CancellationToken>()).Returns(BusinessProfile.Create(Guid.NewGuid(), TenantId, "UTC", "USD"));
        if (featureEnabled)
        {
            features.IsEnabledAsync(TenantId, FeatureCodes.MembershipManagement, Arg.Any<CancellationToken>()).Returns(true);
        }

        return new GetMembershipSalesDistributionQueryHandler(new DashboardAccess(actor, tenants, members, roles, profiles, features, clock), analytics);
    }

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
