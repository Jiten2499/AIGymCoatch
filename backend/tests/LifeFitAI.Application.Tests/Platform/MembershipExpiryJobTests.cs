using FluentAssertions;
using Hangfire;
using Hangfire.InMemory;
using Hangfire.Storage;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Platform;

public sealed class MembershipExpiryJobTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _other = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly DateTime _utc = new(2026, 10, 19, 20, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Messages_UseStableReminderTypes()
    {
        var expiry = new DateOnly(2026, 10, 30);
        MembershipExpiryMessages.Compose(MembershipExpiryReminderTypes.TenDays, expiry).Message.Should().Be("Your gym membership expires in 10 days on 30 Oct 2026.");
        MembershipExpiryMessages.Compose(MembershipExpiryReminderTypes.FiveDays, expiry.AddDays(-5)).Message.Should().Be("Your gym membership expires in 5 days on 25 Oct 2026.");
        MembershipExpiryMessages.Compose(MembershipExpiryReminderTypes.Today, expiry).Message.Should().Be("Your gym membership expires today.");
    }

    [Fact]
    public void DailySchedule_RegistersAndRemovesTheRecurringJob()
    {
        var storage = new InMemoryStorage();
        var scheduler = new HangfireJobScheduler(new BackgroundJobClient(storage), storage);
        var registration = new RecurringJobRegistration(SystemJobCatalog.MembershipExpiryKey, SystemJobCatalog.MembershipExpiryType, SystemJobCatalog.DailyCron, SystemJobCatalog.NotificationsQueue, true);
        scheduler.SyncRecurring(registration);
        using var connection = storage.GetConnection();
        var recurring = connection.GetRecurringJobs().Should().ContainSingle().Subject;
        recurring.Id.Should().Be(SystemJobCatalog.MembershipExpiryKey);
        recurring.Cron.Should().Be("0 1 * * *");
        recurring.Queue.Should().Be("notifications");
        scheduler.SyncRecurring(registration with { IsEnabled = false });
        storage.GetConnection().GetRecurringJobs().Should().BeEmpty();
    }

    [Fact]
    public async Task DisabledJob_DoesNotCreateReminders()
    {
        await using var db = CreateDb();
        SeedWorkspace(db, _tenant, "Asia/Kolkata");
        AddMembership(db, _tenant, Guid.NewGuid(), new DateOnly(2026, 10, 30), Guid.NewGuid(), true);
        db.SystemJobDefinitions.Add(SystemJobDefinition.Create(Guid.NewGuid(), SystemJobCatalog.MembershipExpiryKey, "Membership Expiry Notification", null, SystemJobCatalog.MembershipExpiryType, SystemJobCatalog.DailyCron, SystemJobCatalog.NotificationsQueue, false, FeatureCodes.GymCommunications, _utc));
        await db.SaveChangesAsync();
        var json = await new MembershipExpiryNotificationJob(db, Processor(db, true)).RunAsync(false);
        json.Should().Contain("true");
        (await db.MembershipExpiryReminders.CountAsync()).Should().Be(0);
        (await new MembershipExpiryNotificationJob(db, Processor(db, true)).RunAsync(true)).Should().Contain("true");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Run_CreatesOneReminderPerWindowAndIsolatesTenants()
    {
        await using var db = CreateDb();
        SeedWorkspace(db, _tenant, "Asia/Kolkata");
        SeedWorkspace(db, _other, "UTC");
        var ten = AddMembership(db, _tenant, Guid.NewGuid(), new DateOnly(2026, 10, 30), Guid.NewGuid(), true);
        var five = AddMembership(db, _tenant, Guid.NewGuid(), new DateOnly(2026, 10, 25), Guid.NewGuid(), true);
        five.Freeze(new DateOnly(2026, 10, 20));
        var today = AddMembership(db, _tenant, Guid.NewGuid(), new DateOnly(2026, 10, 20), Guid.NewGuid(), true);
        AddMembership(db, _tenant, Guid.NewGuid(), new DateOnly(2026, 10, 20), null, true);
        var cancelled = AddMembership(db, _tenant, Guid.NewGuid(), new DateOnly(2026, 10, 20), Guid.NewGuid(), true);
        cancelled.Cancel();
        AddMembership(db, _tenant, Guid.NewGuid(), new DateOnly(2026, 10, 19), Guid.NewGuid(), true);
        var pending = GymMembership.Create(Guid.NewGuid(), _tenant, Member(db, _tenant, Guid.NewGuid(), true), null, Guid.NewGuid(), "M-P", new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 20), false, null);
        db.GymMemberships.Add(pending);
        var foreign = AddMembership(db, _other, Guid.NewGuid(), new DateOnly(2026, 10, 29), Guid.NewGuid(), true);
        await db.SaveChangesAsync();

        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenant, FeatureCodes.GymCommunications, Arg.Any<CancellationToken>()).Returns(true);
        features.IsEnabledAsync(_other, FeatureCodes.GymCommunications, Arg.Any<CancellationToken>()).Returns(false);
        var first = await new MembershipExpiryProcessor(db, features, Clock()).ExecuteAsync(CancellationToken.None);
        var second = await new MembershipExpiryProcessor(db, features, Clock()).ExecuteAsync(CancellationToken.None);

        first.TenDay.Should().Be(1);
        first.FiveDay.Should().Be(1);
        first.Today.Should().Be(1);
        first.Skipped.Should().Be(1);
        first.Failed.Should().Be(0);
        second.TenDay.Should().Be(0);
        (await db.MembershipExpiryReminders.CountAsync()).Should().Be(3);
        (await db.OutboxMessages.CountAsync()).Should().Be(3);
        (await db.MembershipExpiryReminders.AnyAsync(reminder => reminder.MembershipId == foreign.Id)).Should().BeFalse();
        (await db.MembershipExpiryReminders.Select(reminder => reminder.ReminderType).ToListAsync()).Should().BeEquivalentTo(
            [MembershipExpiryReminderTypes.TenDays, MembershipExpiryReminderTypes.FiveDays, MembershipExpiryReminderTypes.Today]);
        db.Model.FindEntityType(typeof(MembershipExpiryReminder))!.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Count == 4);

        await Dispatch(db);
        (await db.Notifications.CountAsync()).Should().Be(3);
        (await db.Notifications.Select(item => item.Type).Distinct().ToListAsync()).Should().Equal(NotificationType.MembershipReminder);
        await Dispatch(db);
        (await db.Notifications.CountAsync()).Should().Be(3);
        (await db.Notifications.CountAsync(item => item.Message.Contains("10 days"))).Should().Be(1);
        (await db.Notifications.CountAsync(item => item.Message == "Your gym membership expires today.")).Should().Be(1);
        ten.Id.Should().NotBe(today.Id);
    }

    [Fact]
    public void Statistics_ComeOnlyFromExecutionResult()
    {
        var raw = System.Text.Json.JsonSerializer.Serialize(new MembershipExpiryRun(4, 1, 1, 1, 1, 0, false));
        var stats = SystemJobStatisticsReader.Read(System.Text.Json.JsonSerializer.Serialize(raw));
        stats!.MembersProcessed.Should().Be(4);
        stats.TenDay.Should().Be(1);
        SystemJobStatisticsReader.Read(System.Text.Json.JsonSerializer.Serialize(new MembershipExpiryRun(0, 0, 0, 0, 0, 0, true))).Should().BeNull();
        SystemJobCatalog.DescribeSchedule(SystemJobCatalog.DailyCron).Should().Be("Daily at 01:00 UTC");
    }

    private async Task Dispatch(ApplicationDbContext db)
    {
        var scopes = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var provider = Substitute.For<IServiceProvider>();
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => db.SaveChangesAsync());
        var clock = Clock();
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(Arg.Any<Guid>(), FeatureCodes.GymCommunications, Arg.Any<CancellationToken>()).Returns(true);
        provider.GetService(typeof(IOutboxStore)).Returns(new OutboxStore(db));
        provider.GetService(typeof(IBackgroundJobScheduler)).Returns(Substitute.For<IBackgroundJobScheduler>());
        provider.GetService(typeof(IDateTimeProvider)).Returns(clock);
        provider.GetService(typeof(IUnitOfWork)).Returns(unit);
        provider.GetService(typeof(IFeatureFlagService)).Returns(features);
        provider.GetService(typeof(ApplicationDbContext)).Returns(db);
        scope.ServiceProvider.Returns(provider);
        scopes.CreateScope().Returns(scope);
        await new OutboxDispatcher(scopes, NullLogger<OutboxDispatcher>.Instance).DispatchOnceAsync(CancellationToken.None);
    }

    private MembershipExpiryProcessor Processor(ApplicationDbContext db, bool enabled)
    {
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(enabled);
        return new MembershipExpiryProcessor(db, features, Clock());
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return clock;
    }

    private static void SeedWorkspace(ApplicationDbContext db, Guid tenantId, string timeZone)
    {
        db.Tenants.Add(Tenant.Create(tenantId, tenantId.ToString(), TenantType.Gym));
        db.BusinessProfiles.Add(BusinessProfile.Create(Guid.NewGuid(), tenantId, timeZone, "INR"));
    }

    private static Guid Member(ApplicationDbContext db, Guid tenantId, Guid? userId, bool active)
    {
        var member = MemberProfile.Create(Guid.NewGuid(), tenantId, userId, Guid.NewGuid().ToString("N")[..8], new DateOnly(2026, 1, 1), "A", "B");
        if (!active)
        {
            member.Deactivate(DateTime.UtcNow);
        }

        db.MemberProfiles.Add(member);
        return member.Id;
    }

    private GymMembership AddMembership(ApplicationDbContext db, Guid tenantId, Guid membershipId, DateOnly end, Guid? userId, bool active)
    {
        var membership = GymMembership.Create(membershipId, tenantId, Member(db, tenantId, userId, active), null, Guid.NewGuid(), membershipId.ToString("N")[..8], new DateOnly(2026, 10, 1), end, new DateOnly(2026, 10, 1), false, null);
        db.GymMemberships.Add(membership);
        return membership;
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
