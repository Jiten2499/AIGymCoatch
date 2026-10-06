using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class BusinessDashboardKpiTests
{
    private static readonly DateTime UtcNow = new(2026, 10, 4, 19, 0, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    [Fact]
    public void Period_UsesTenantLocalCalendar()
    {
        var period = DashboardPeriod.For(UtcNow, India);

        period.Today.Should().Be(new DateOnly(2026, 10, 5));
        period.MonthStart.Should().Be(new DateOnly(2026, 10, 1));
        period.ExpiringThrough7Days.Should().Be(new DateOnly(2026, 10, 12));
        period.ExpiringThrough30Days.Should().Be(new DateOnly(2026, 11, 4));
        period.TodayStartUtc.Should().Be(new DateTime(2026, 10, 4, 18, 30, 0, DateTimeKind.Utc));
        period.TomorrowStartUtc.Should().Be(period.TodayStartUtc.AddDays(1));
    }

    [Fact]
    public async Task Counts_EmptyTenant_ReturnsZeros()
    {
        await using var db = CreateDb();
        var counts = await new DashboardQuery(db).CountAsync(Guid.NewGuid(), UtcNow, India, All, CancellationToken.None);

        counts.Should().Be(new BusinessKpiCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Counts_MembersAttendanceMembershipsAndPeople_AreTenantScoped()
    {
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using var db = CreateDb();
        var today = new DateOnly(2026, 10, 5);
        SeedMembers(db, tenant, other, today);
        SeedAttendance(db, tenant, other);
        SeedMemberships(db, tenant, other, today);
        SeedPeople(db, tenant, other);
        await db.SaveChangesAsync();

        var counts = await new DashboardQuery(db).CountAsync(tenant, UtcNow, India, All, CancellationToken.None);

        counts.TotalMembers.Should().Be(3);
        counts.ActiveMembers.Should().Be(2);
        counts.NewMembersThisMonth.Should().Be(1);
        counts.CheckInsToday.Should().Be(1);
        counts.ActiveMemberships.Should().Be(3);
        counts.ExpiringIn7Days.Should().Be(2);
        counts.ExpiringIn30Days.Should().Be(3);
        counts.ExpiredMemberships.Should().Be(2);
        counts.ActiveTrainers.Should().Be(1);
        counts.ActiveStaff.Should().Be(1);
    }

    private static void SeedMembers(ApplicationDbContext db, Guid tenant, Guid other, DateOnly today)
    {
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "A", new DateOnly(2026, 10, 2)));
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "B", new DateOnly(2026, 9, 2)));
        var inactive = MemberProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "C", today);
        inactive.Update(null, null, null, null, null, null, null, null, ProfileStatus.Inactive);
        db.MemberProfiles.Add(inactive);
        var removed = MemberProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "D", today);
        removed.Deactivate(UtcNow);
        db.MemberProfiles.Add(removed);
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), other, Guid.NewGuid(), "E", today));
    }

    private static void SeedAttendance(ApplicationDbContext db, Guid tenant, Guid other)
    {
        var todayStart = new DateTime(2026, 10, 4, 18, 30, 0, DateTimeKind.Utc);
        db.Attendance.Add(Visit(tenant, todayStart));
        db.Attendance.Add(Visit(tenant, todayStart.AddMinutes(-1)));
        db.Attendance.Add(Visit(other, todayStart));
    }

    private static void SeedMemberships(ApplicationDbContext db, Guid tenant, Guid other, DateOnly today)
    {
        db.GymMemberships.Add(Plan(tenant, today, 1));
        db.GymMemberships.Add(Plan(tenant, today, 8));
        db.GymMemberships.Add(Plan(tenant, today, 31));
        db.GymMemberships.Add(Plan(tenant, today.AddDays(-10), 1));
        var expired = Plan(tenant, today.AddDays(-20), 5);
        expired.Expire(today);
        db.GymMemberships.Add(expired);
        var cancelled = Plan(tenant, today.AddDays(-8), 2);
        cancelled.Cancel();
        db.GymMemberships.Add(cancelled);
        var frozen = Plan(tenant, today, 3);
        frozen.Freeze(today);
        db.GymMemberships.Add(frozen);
        db.GymMemberships.Add(Plan(other, today, 1));
    }

    private static void SeedPeople(ApplicationDbContext db, Guid tenant, Guid other)
    {
        db.TrainerProfiles.Add(TrainerProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), null));
        var inactiveTrainer = TrainerProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), null);
        inactiveTrainer.Update(null, null, null, null, null, null, ProfileStatus.Inactive);
        db.TrainerProfiles.Add(inactiveTrainer);
        var removedTrainer = TrainerProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), null);
        removedTrainer.Remove(UtcNow);
        db.TrainerProfiles.Add(removedTrainer);
        db.TrainerProfiles.Add(TrainerProfile.Create(Guid.NewGuid(), other, Guid.NewGuid(), null));

        db.StaffProfiles.Add(StaffProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "S1", null, StaffType.Receptionist, todayJoin));
        var inactiveStaff = StaffProfile.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), "S2", null, StaffType.Manager, todayJoin);
        inactiveStaff.Update(null, StaffType.Manager, ProfileStatus.Inactive);
        db.StaffProfiles.Add(inactiveStaff);
        db.StaffProfiles.Add(StaffProfile.Create(Guid.NewGuid(), other, Guid.NewGuid(), "S3", null, StaffType.Receptionist, todayJoin));
    }

    private static readonly DateOnly todayJoin = new(2026, 10, 5);

    private static AttendanceRecord Visit(Guid tenantId, DateTime checkIn) =>
        AttendanceRecord.CheckIn(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CheckInMethod.Manual, checkIn);

    private static GymMembership Plan(Guid tenantId, DateOnly start, int durationDays) =>
        GymMembership.Create(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("N")[..8], start, durationDays, new DateOnly(2026, 10, 5), false, null);

    private static readonly KpiScope All = new(true, true, true, true);

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
