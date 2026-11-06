using FluentAssertions;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class DashboardBoardTests
{
    [Fact]
    public void Revenue_RollsConfirmedAmountsIntoTheSelectedRange()
    {
        var calendar = ResolvedAnalytics.Resolve(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), AnalyticsPeriod.Week, TimeZoneInfo.Utc).Value;
        var points = RevenueSeries.Rollup(calendar,
        [
            new DateAmount(new DateOnly(2026, 10, 1), 40m),
            new DateAmount(new DateOnly(2026, 10, 3), 10m),
            new DateAmount(new DateOnly(2026, 10, 8), 99m),
        ]);

        points.Should().HaveCount(2);
        points[0].Amount.Should().Be(50m);
        points[1].Amount.Should().Be(0m);
        RevenueSeries.Collected(points).Should().Be(50m);
        RevenueSeries.Collected(RevenueSeries.Rollup(calendar, [])).Should().Be(0m);
    }

    [Fact]
    public async Task Payments_SumPostedAmountsForTheTenantCurrencyAndRange()
    {
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        var day = new DateOnly(2026, 10, 2);
        await using var db = CreateDb();
        var member = MemberProfile.Create(Guid.NewGuid(), tenant, null, "M1", day, "Ada", "Lovelace");
        db.MemberProfiles.Add(member);
        var owed = Membership(db, tenant, member.Id, day, 100m);
        var paid = Membership(db, tenant, member.Id, day, 40m);
        var cancelled = Membership(db, tenant, member.Id, day, 80m);
        cancelled.Cancel();
        var later = Membership(db, tenant, member.Id, day.AddDays(10), 70m);
        db.MembershipPayments.Add(Payment(tenant, member.Id, owed.Id, 25m, "USD", day));
        var voided = Payment(tenant, member.Id, owed.Id, 75m, "USD", day);
        voided.Void(Guid.NewGuid(), DateTime.UtcNow);
        db.MembershipPayments.Add(voided);
        db.MembershipPayments.Add(Payment(tenant, member.Id, paid.Id, 40m, "USD", day));
        db.MembershipPayments.Add(Payment(tenant, member.Id, owed.Id, 10m, "EUR", day));
        db.MembershipPayments.Add(Payment(tenant, member.Id, owed.Id, 30m, "USD", day.AddDays(5)));
        db.MembershipPayments.Add(Payment(other, member.Id, owed.Id, 90m, "USD", day));
        var inactive = MemberProfile.Create(Guid.NewGuid(), tenant, null, "M2", day, "Grace", "Hopper");
        inactive.Update(null, null, null, null, null, null, null, null, ProfileStatus.Inactive);
        db.MemberProfiles.Add(inactive);
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), other, null, "M3", day, "Other", "Gym"));
        await db.SaveChangesAsync();

        var store = new DashboardBoardStore(db);
        var payments = await store.PostedPaymentsAsync(tenant, "USD", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), CancellationToken.None);
        payments.Sum(item => item.Amount).Should().Be(65m);
        (await store.PostedPaymentsAsync(other, "USD", day, day, CancellationToken.None)).Sum(item => item.Amount).Should().Be(90m);
        (await store.CountOverdueMembershipsAsync(tenant, day, CancellationToken.None)).Should().Be(1);
        (await store.CountInactiveMembersAsync(tenant, CancellationToken.None)).Should().Be(1);
        (await store.CountInactiveMembersAsync(Guid.NewGuid(), CancellationToken.None)).Should().Be(0);
        later.FinalAmount.Should().Be(70m);
    }

    [Fact]
    public async Task Operations_KeepTodayInsideTheTenantAndIgnoreOtherGyms()
    {
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        var day = new DateOnly(2026, 10, 28);
        var start = new DateTime(2026, 10, 28, 4, 0, 0, DateTimeKind.Utc);
        var next = start.AddDays(1);
        await using var db = CreateDb();
        var member = MemberProfile.Create(Guid.NewGuid(), tenant, null, "M1", day, "Ada", "Lovelace");
        db.MemberProfiles.Add(member);
        var lead = Lead.Create(Guid.NewGuid(), tenant, "Priya Shah", "9000000000", null, LeadSource.WalkIn, null);
        db.Leads.Add(lead);
        db.LeadFollowUps.Add(LeadFollowUp.Create(Guid.NewGuid(), tenant, lead.Id, start.AddHours(-30), null, null, start));
        db.LeadFollowUps.Add(LeadFollowUp.Create(Guid.NewGuid(), tenant, lead.Id, start.AddHours(2), null, null, start));
        db.LeadFollowUps.Add(LeadFollowUp.Create(Guid.NewGuid(), other, lead.Id, start.AddHours(-5), null, null, start));
        var session = PtSession.Schedule(Guid.NewGuid(), tenant, member.Id, Guid.NewGuid(), start.AddHours(3), start.AddHours(4), "PT", null, null);
        db.PtSessions.Add(session!);
        var cancelled = PtSession.Schedule(Guid.NewGuid(), tenant, member.Id, Guid.NewGuid(), start.AddHours(5), start.AddHours(6), "PT", null, null);
        cancelled!.Cancel();
        db.PtSessions.Add(cancelled);
        await db.SaveChangesAsync();

        var store = new DashboardBoardStore(db);
        (await store.CountFollowUpsAsync(tenant, next, CancellationToken.None)).Should().Be(2);
        (await store.CountFollowUpsAsync(tenant, start, CancellationToken.None)).Should().Be(1);
        (await store.UpcomingSessionsAsync(tenant, start, next, CancellationToken.None)).Should().ContainSingle(item => item.MemberName == "Ada Lovelace");
        (await store.ActivitiesAsync(tenant, start, next, CancellationToken.None)).Should().ContainSingle(item => item.Title == "Priya Shah");
        (await store.UpcomingSessionsAsync(other, start, next, CancellationToken.None)).Should().BeEmpty();
    }

    private static GymMembership Membership(ApplicationDbContext db, Guid tenantId, Guid memberId, DateOnly start, decimal amount)
    {
        var membership = GymMembership.Create(Guid.NewGuid(), tenantId, memberId, null, Guid.NewGuid(), $"G{Guid.NewGuid():N}"[..8], start, 30, start, false, null);
        membership.SetPricing(amount, 0m, amount, "USD", null, null);
        db.GymMemberships.Add(membership);
        return membership;
    }

    private static MembershipPayment Payment(Guid tenantId, Guid memberId, Guid membershipId, decimal amount, string currency, DateOnly day) =>
        MembershipPayment.Create(Guid.NewGuid(), tenantId, memberId, membershipId, amount, currency, MembershipPaymentMethod.Cash, day, Guid.NewGuid(), null, null);

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
