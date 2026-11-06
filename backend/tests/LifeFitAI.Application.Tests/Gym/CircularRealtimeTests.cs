using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
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

public sealed class CircularRealtimeTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _other = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly DateTime _utc = new(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Published_QueuesOneNoticeWithoutTheBody_AndARetryDoesNotQueueAnother()
    {
        await using var db = CreateDb();
        var member = Guid.NewGuid();
        db.MemberProfiles.Add(Member(member));
        var communication = Published("Secret body");
        db.Communications.Add(communication);
        await db.SaveChangesAsync();
        var delivery = new CommunicationDelivery(db);

        await delivery.DeliverAsync(communication, _utc, CancellationToken.None);
        await delivery.DeliverAsync(communication, _utc, CancellationToken.None);
        await db.SaveChangesAsync();

        var notice = await db.OutboxMessages.SingleAsync();
        notice.MessageType.Should().Be(CircularPublication.MessageType);
        notice.IdempotencyKey.Should().Be(CircularPublication.Key(communication.Id));
        notice.Payload.Should().Contain(communication.Id.ToString());
        notice.Payload.Should().NotContain("Secret body");
        notice.TenantId.Should().Be(_tenant);
    }

    [Fact]
    public async Task DraftAndFutureSchedule_DoNotQueueABroadcast()
    {
        await using var db = CreateDb();
        db.MemberProfiles.Add(Member(Guid.NewGuid()));
        var draft = Communication.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), CommunicationType.Circular, "Draft", "Secret body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, null, null);
        var scheduled = Communication.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), CommunicationType.Circular, "Later", "Secret body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, _utc.AddHours(2), null);
        scheduled.Publish(_utc);
        db.Communications.AddRange(draft, scheduled);
        await db.SaveChangesAsync();
        var delivery = new CommunicationDelivery(db);

        await delivery.DeliverAsync(draft, _utc, CancellationToken.None);
        await delivery.DeliverAsync(scheduled, _utc, CancellationToken.None);
        await db.SaveChangesAsync();

        (await db.OutboxMessages.CountAsync(item => item.MessageType == CircularPublication.MessageType)).Should().Be(0);
        scheduled.Status.Should().Be(CommunicationStatus.Scheduled);
    }

    [Fact]
    public async Task ScheduledPublisher_BroadcastsOnlyAfterPublication_AndARetryDoesNotDuplicate()
    {
        await using var db = CreateDb();
        db.Tenants.Add(Tenant.Create(_tenant, "ABC Fitness", TenantType.Gym));
        var member = Guid.NewGuid();
        db.MemberProfiles.Add(Member(member));
        var due = Scheduled(db, _utc.AddMinutes(-5));
        var waiting = Scheduled(db, _utc.AddHours(2));
        await db.SaveChangesAsync();
        var publisher = Publisher(db, _utc);

        await publisher.ExecuteAsync(due.Id, _tenant, "corr", due.PublishAt!.Value, CancellationToken.None);
        await publisher.ExecuteAsync(due.Id, _tenant, "corr", due.PublishAt!.Value, CancellationToken.None);
        await publisher.ExecuteAsync(waiting.Id, _tenant, "corr", waiting.PublishAt!.Value, CancellationToken.None);

        due.Status.Should().Be(CommunicationStatus.Published);
        waiting.Status.Should().Be(CommunicationStatus.Scheduled);
        var notices = await db.OutboxMessages.Where(item => item.MessageType == CircularPublication.MessageType).ToListAsync();
        notices.Should().ContainSingle();
        notices[0].IdempotencyKey.Should().Be(CircularPublication.Key(due.Id));
        notices[0].Payload.Should().NotContain("Secret body");
    }

    [Fact]
    public async Task Dispatcher_SendsEachAudienceOnceInsideTheTenant()
    {
        await using var db = CreateDb();
        var member = Guid.NewGuid();
        var trainer = Guid.NewGuid();
        var staff = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        db.MemberProfiles.Add(Member(member));
        db.TrainerProfiles.Add(TrainerProfile.Create(Guid.NewGuid(), _tenant, trainer, null));
        db.StaffProfiles.Add(StaffProfile.Create(Guid.NewGuid(), _tenant, staff, "S-1", null, StaffType.Receptionist, DateOnly.FromDateTime(_utc)));
        var memberNotice = await PublishAudience(db, CommunicationAudienceType.AllMembers);
        var trainerNotice = await PublishAudience(db, CommunicationAudienceType.AllTrainers);
        var staffNotice = await PublishAudience(db, CommunicationAudienceType.AllStaff);
        db.CommunicationRecipients.Add(CommunicationRecipient.Create(Guid.NewGuid(), _other, memberNotice, outsider, CommunicationRecipientKind.Member, null, _utc));
        await db.SaveChangesAsync();
        var notifier = Substitute.For<ICircularNotifier>();

        await Dispatch(db, notifier, enabled: true);
        await Dispatch(db, notifier, enabled: true);

        await notifier.Received(1).PublishedAsync(
            Arg.Is<CircularPublishedNotice>(item => item.CommunicationId == memberNotice && item.TenantId == _tenant && item.Audience == "AllMembers"),
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { member })),
            Arg.Any<CancellationToken>());
        await notifier.Received(1).PublishedAsync(
            Arg.Is<CircularPublishedNotice>(item => item.CommunicationId == trainerNotice && item.Audience == "AllTrainers"),
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { trainer })),
            Arg.Any<CancellationToken>());
        await notifier.Received(1).PublishedAsync(
            Arg.Is<CircularPublishedNotice>(item => item.CommunicationId == staffNotice && item.Audience == "AllStaff"),
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { staff })),
            Arg.Any<CancellationToken>());
        (await db.OutboxMessages.CountAsync(item => item.Status == OutboxMessageStatus.Dispatched)).Should().Be(3);
    }

    [Fact]
    public async Task Dispatcher_SkipsArchivedExpiredAndDisabledCirculars()
    {
        await using var db = CreateDb();
        var member = Guid.NewGuid();
        db.MemberProfiles.Add(Member(member));
        var archived = Published("Secret body");
        var expired = Published("Secret body", _utc.AddMinutes(-1));
        db.Communications.AddRange(archived, expired);
        await db.SaveChangesAsync();
        var delivery = new CommunicationDelivery(db);
        await delivery.DeliverAsync(archived, _utc, CancellationToken.None);
        await delivery.DeliverAsync(expired, _utc, CancellationToken.None);
        archived.Archive(_utc);
        await db.SaveChangesAsync();
        var notifier = Substitute.For<ICircularNotifier>();

        await Dispatch(db, notifier, enabled: true);
        var hidden = Published("Secret body");
        db.Communications.Add(hidden);
        await db.SaveChangesAsync();
        await new CommunicationDelivery(db).DeliverAsync(hidden, _utc, CancellationToken.None);
        await db.SaveChangesAsync();
        await Dispatch(db, notifier, enabled: false);

        await notifier.DidNotReceive().PublishedAsync(Arg.Any<CircularPublishedNotice>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
        (await db.OutboxMessages.CountAsync(item => item.Status == OutboxMessageStatus.Cancelled)).Should().Be(3);
    }

    [Fact]
    public async Task OperatorGroup_UsesTheAuthenticatedTenant_AndIgnoresReaders()
    {
        var userId = Guid.NewGuid();
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(userId);
        actor.TenantId.Returns(_tenant);
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(userId, _tenant, PermissionCodes.CommunicationCreate, Arg.Any<CancellationToken>()).Returns(true);
        var audience = new CircularAudience(actor, permissions);

        (await audience.OperatorGroupAsync(CancellationToken.None)).Should().Be(CircularGroups.Operators(_tenant));
        CircularGroups.User(userId).Should().Be($"user:{userId:D}");

        permissions.HasPermissionAsync(userId, _tenant, PermissionCodes.CommunicationCreate, Arg.Any<CancellationToken>()).Returns(false);
        (await audience.OperatorGroupAsync(CancellationToken.None)).Should().BeNull();
        actor.TenantId.Returns((Guid?)null);
        (await audience.OperatorGroupAsync(CancellationToken.None)).Should().BeNull();
    }

    private async Task<Guid> PublishAudience(ApplicationDbContext db, CommunicationAudienceType audience)
    {
        var communication = Published("Secret body", null, audience);
        db.Communications.Add(communication);
        await db.SaveChangesAsync();
        await new CommunicationDelivery(db).DeliverAsync(communication, _utc, CancellationToken.None);
        await db.SaveChangesAsync();
        return communication.Id;
    }

    private async Task Dispatch(ApplicationDbContext db, ICircularNotifier notifier, bool enabled)
    {
        var scopes = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var provider = Substitute.For<IServiceProvider>();
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => db.SaveChangesAsync());
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenant, FeatureCodes.GymCommunications, Arg.Any<CancellationToken>()).Returns(enabled);
        provider.GetService(typeof(IOutboxStore)).Returns(new OutboxStore(db));
        provider.GetService(typeof(IBackgroundJobScheduler)).Returns(Substitute.For<IBackgroundJobScheduler>());
        provider.GetService(typeof(IDateTimeProvider)).Returns(clock);
        provider.GetService(typeof(IUnitOfWork)).Returns(unit);
        provider.GetService(typeof(IFeatureFlagService)).Returns(features);
        provider.GetService(typeof(ApplicationDbContext)).Returns(db);
        provider.GetService(typeof(ICircularNotifier)).Returns(notifier);
        scope.ServiceProvider.Returns(provider);
        scopes.CreateScope().Returns(scope);
        await new OutboxDispatcher(scopes, Substitute.For<Microsoft.Extensions.Logging.ILogger<OutboxDispatcher>>()).DispatchOnceAsync(CancellationToken.None);
    }

    private MemberProfile Member(Guid userId) =>
        MemberProfile.Create(Guid.NewGuid(), _tenant, userId, $"M-{userId:N}"[..12], DateOnly.FromDateTime(_utc), "Ada", "Member");

    private Communication Published(string message, DateTime? expiresAt = null, CommunicationAudienceType audience = CommunicationAudienceType.AllMembers)
    {
        var communication = Communication.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), CommunicationType.Circular, "Notice", message, CommunicationPriority.Normal, audience, null, null, null, null, expiresAt);
        communication.Publish(_utc.AddMinutes(-2), immediate: true);
        return communication;
    }

    private Communication Scheduled(ApplicationDbContext db, DateTime publishAt)
    {
        var communication = Communication.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), CommunicationType.Circular, "Notice", "Secret body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, publishAt, null);
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
