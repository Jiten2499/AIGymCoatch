using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class CommunicationScheduleTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _other = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly DateTime _utc = new(2026, 10, 8, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Normalize_ConvertsTenantLocalTimeAndRejectsThePast()
    {
        var future = CommunicationScheduleRules.Normalize(new DateTime(2026, 10, 15, 10, 0, 0), new DateTime(2026, 10, 20, 23, 59, 0), true, _utc, "Asia/Kolkata");
        future.IsSuccess.Should().BeTrue();
        future.Value.PublishAtUtc.Should().Be(new DateTime(2026, 10, 15, 4, 30, 0, DateTimeKind.Utc));
        future.Value.ExpiresAtUtc.Should().Be(new DateTime(2026, 10, 20, 18, 29, 0, DateTimeKind.Utc));

        var past = CommunicationScheduleRules.Normalize(new DateTime(2026, 10, 8, 8, 0, 0), null, true, _utc, "Asia/Kolkata");
        past.Error.Should().Be(GymErrors.InvalidState);
        CommunicationScheduleRules.Normalize(new DateTime(2026, 10, 15, 10, 0, 0), new DateTime(2026, 10, 15, 9, 0, 0), true, _utc, "Asia/Kolkata").IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Schedule_WritesOneOutboxRowAndRescheduleReplacesTheJob()
    {
        await using var db = CreateDb();
        db.Tenants.Add(Tenant.Create(_tenant, "ABC Fitness", TenantType.Gym));
        db.BusinessProfiles.Add(BusinessProfile.Create(Guid.NewGuid(), _tenant, "Asia/Kolkata", "INR"));
        await db.SaveChangesAsync();
        var scheduler = Substitute.For<IBackgroundJobScheduler>();
        scheduler.Schedule<PublishCommunicationJob>(Arg.Any<BackgroundJobMetadata>(), Arg.Any<System.Linq.Expressions.Expression<Func<PublishCommunicationJob, Task>>>(), Arg.Any<DateTime>()).Returns("job-1", "job-2");
        var schedule = Service(db, scheduler);
        var communication = Communication.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), CommunicationType.Announcement, "Notice", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, null, null);
        var times = await schedule.NormalizeAsync(_tenant, new DateTime(2026, 10, 15, 10, 0, 0), null, true, CancellationToken.None);
        communication.UpdateDraft(communication.Type, communication.Title, communication.Message, communication.Priority, communication.AudienceType, null, null, null, times.Value.PublishAtUtc, null);
        communication.Publish(_utc);
        db.Communications.Add(communication);
        await schedule.SyncAsync(communication, CancellationToken.None);
        await db.SaveChangesAsync();

        var row = await db.OutboxMessages.SingleAsync();
        row.IdempotencyKey.Should().Be(OutboxMessage.PublishKey(communication.Id));
        row.Status.Should().Be(OutboxMessageStatus.Pending);
        row.AvailableAtUtc.Should().Be(new DateTime(2026, 10, 15, 4, 30, 0));
    }

    [Fact]
    public async Task Dispatcher_SchedulesHangfireWithoutASecondOutboxRow()
    {
        await using var db = CreateDb();
        var communicationId = Guid.NewGuid();
        var payload = System.Text.Json.JsonSerializer.Serialize(new CommunicationPublishPayload(communicationId, _tenant, "corr", _utc.AddDays(1), null));
        db.OutboxMessages.Add(OutboxMessage.Create(Guid.NewGuid(), _tenant, "Communication.Publish", payload, _utc.AddDays(1), "corr", OutboxMessage.PublishKey(communicationId), _utc));
        await db.SaveChangesAsync();
        var scheduler = Substitute.For<IBackgroundJobScheduler>();
        scheduler.Schedule<PublishCommunicationJob>(Arg.Any<BackgroundJobMetadata>(), Arg.Any<System.Linq.Expressions.Expression<Func<PublishCommunicationJob, Task>>>(), Arg.Any<DateTime>()).Returns("job-1");
        var scopes = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var provider = Substitute.For<IServiceProvider>();
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => db.SaveChangesAsync());
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        provider.GetService(typeof(IOutboxStore)).Returns(new OutboxStore(db));
        provider.GetService(typeof(IBackgroundJobScheduler)).Returns(scheduler);
        provider.GetService(typeof(IDateTimeProvider)).Returns(clock);
        provider.GetService(typeof(IUnitOfWork)).Returns(unit);
        scope.ServiceProvider.Returns(provider);
        scopes.CreateScope().Returns(scope);

        await new OutboxDispatcher(scopes, Substitute.For<Microsoft.Extensions.Logging.ILogger<OutboxDispatcher>>()).DispatchOnceAsync(CancellationToken.None);

        (await db.OutboxMessages.SingleAsync()).Status.Should().Be(OutboxMessageStatus.Dispatched);
        scheduler.Received(1).Schedule<PublishCommunicationJob>(
            Arg.Is<BackgroundJobMetadata>(item => item.JobType == "Communication.Publish" && item.SourceType == "Communication" && item.SourceId == communicationId.ToString("D")),
            Arg.Any<System.Linq.Expressions.Expression<Func<PublishCommunicationJob, Task>>>(),
            Arg.Any<DateTime>());
    }

    [Fact]
    public async Task PublishJob_SnapshotsRecipientsOnce_AndIgnoresCancelExpiryAndOtherTenants()
    {
        await using var db = CreateDb();
        db.Tenants.Add(Tenant.Create(_tenant, "ABC Fitness", TenantType.Gym));
        var userId = Guid.NewGuid();
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), _tenant, userId, "M-1", new DateOnly(2026, 1, 1), "Ada", "Member"));
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), _tenant, userId, "M-2", new DateOnly(2026, 1, 1), "Ada", "Duplicate"));
        var due = Scheduled(db, _utc.AddMinutes(-5));
        var waiting = Scheduled(db, _utc.AddHours(2));
        var expired = Communication.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), CommunicationType.Announcement, "Old", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, _utc.AddMinutes(-30), _utc.AddMinutes(-1));
        expired.Publish(_utc.AddHours(-1));
        var foreign = Communication.Create(Guid.NewGuid(), _other, Guid.NewGuid(), CommunicationType.Announcement, "Other", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, _utc.AddMinutes(-5), null);
        foreign.Publish(_utc.AddHours(-1));
        db.Communications.Add(expired);
        db.Communications.Add(foreign);
        await db.SaveChangesAsync();
        var publisher = Publisher(db, _utc);

        await publisher.ExecuteAsync(due.Id, _tenant, "corr", due.PublishAt!.Value, CancellationToken.None);
        await publisher.ExecuteAsync(due.Id, _tenant, "corr", due.PublishAt!.Value, CancellationToken.None);
        await publisher.ExecuteAsync(waiting.Id, _tenant, "corr", waiting.PublishAt!.Value, CancellationToken.None);
        await publisher.ExecuteAsync(expired.Id, _tenant, "corr", expired.PublishAt!.Value, CancellationToken.None);
        await publisher.ExecuteAsync(foreign.Id, _other, "corr", foreign.PublishAt!.Value, CancellationToken.None);

        due.Status.Should().Be(CommunicationStatus.Published);
        (await db.CommunicationRecipients.CountAsync(item => item.CommunicationId == due.Id)).Should().Be(1);
        waiting.Status.Should().Be(CommunicationStatus.Scheduled);
        expired.Status.Should().Be(CommunicationStatus.Scheduled);
        foreign.Status.Should().Be(CommunicationStatus.Scheduled);
    }

    [Fact]
    public async Task CancelAndPublishNow_StopTheScheduledJobFromPublishing()
    {
        await using var db = CreateDb();
        db.Tenants.Add(Tenant.Create(_tenant, "ABC Fitness", TenantType.Gym));
        var userId = Guid.NewGuid();
        db.MemberProfiles.Add(MemberProfile.Create(Guid.NewGuid(), _tenant, userId, "M-1", new DateOnly(2026, 1, 1), "Ada", "Member"));
        var cancelled = Scheduled(db, _utc.AddMinutes(-1));
        cancelled.Archive(_utc);
        var manual = Scheduled(db, _utc.AddDays(1));
        manual.Publish(_utc, immediate: true);
        await db.SaveChangesAsync();
        var publisher = Publisher(db, _utc.AddMinutes(5));

        await publisher.ExecuteAsync(cancelled.Id, _tenant, "corr", _utc.AddMinutes(-1), CancellationToken.None);
        await publisher.ExecuteAsync(manual.Id, _tenant, "corr", _utc.AddDays(1), CancellationToken.None);

        (await db.CommunicationRecipients.CountAsync()).Should().Be(0);
        cancelled.Status.Should().Be(CommunicationStatus.Archived);
        manual.Status.Should().Be(CommunicationStatus.Published);
    }

    private Communication Scheduled(ApplicationDbContext db, DateTime publishAt)
    {
        var communication = Communication.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), CommunicationType.Announcement, "Notice", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, publishAt, null);
        communication.Publish(_utc.AddDays(-1));
        db.Communications.Add(communication);
        return communication;
    }

    private ScheduledCommunicationPublisher Publisher(ApplicationDbContext db, DateTime utcNow)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(utcNow);
        return new ScheduledCommunicationPublisher(db, new CommunicationDelivery(db), new AuditLogRepository(db), clock);
    }

    private CommunicationScheduleService Service(ApplicationDbContext db, IBackgroundJobScheduler scheduler)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return new CommunicationScheduleService(new BusinessProfileRepository(db), new TenantRepository(db), new OutboxStore(db), scheduler, clock);
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
