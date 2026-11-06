using FluentAssertions;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class CommunicationDeliveryTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _otherTenant = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly DateTime _utc = new(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AllMembers_IncludesAccountHoldersAndSkipsMembersWithoutUsers()
    {
        await using var db = CreateDb();
        var withAccount = Member(_tenant, Guid.NewGuid(), "M-1");
        var withoutAccount = MemberProfile.Create(Guid.NewGuid(), _tenant, null, "M-2", DateOnly.FromDateTime(_utc), "No", "Account");
        db.MemberProfiles.AddRange(withAccount, withoutAccount);
        await db.SaveChangesAsync();

        var preview = await new CommunicationDelivery(db).PreviewAsync(_tenant, CommunicationAudienceType.AllMembers, null, null, null, [], [], [], _utc, CancellationToken.None);

        preview.Members.Should().Be(1);
        preview.Total.Should().Be(1);
    }

    [Fact]
    public async Task AllTrainers_AllStaff_AndAllBusinessUsers_AreTenantScoped()
    {
        await using var db = CreateDb();
        var trainerUser = Guid.NewGuid();
        var staffUser = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        db.TrainerProfiles.Add(TrainerProfile.Create(Guid.NewGuid(), _tenant, trainerUser, null));
        db.TrainerProfiles.Add(TrainerProfile.Create(Guid.NewGuid(), _otherTenant, outsider, null));
        db.StaffProfiles.Add(StaffProfile.Create(Guid.NewGuid(), _tenant, staffUser, "S-1", null, StaffType.Receptionist, DateOnly.FromDateTime(_utc)));
        db.Users.Add(User(trainerUser));
        db.Users.Add(User(staffUser));
        db.Users.Add(User(outsider));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, trainerUser, _utc));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, staffUser, _utc));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _otherTenant, outsider, _utc));
        await db.SaveChangesAsync();

        var delivery = new CommunicationDelivery(db);
        (await delivery.PreviewAsync(_tenant, CommunicationAudienceType.AllTrainers, null, null, null, [], [], [], _utc, CancellationToken.None)).Trainers.Should().Be(1);
        (await delivery.PreviewAsync(_tenant, CommunicationAudienceType.AllStaff, null, null, null, [], [], [], _utc, CancellationToken.None)).Staff.Should().Be(1);
        (await delivery.PreviewAsync(_tenant, CommunicationAudienceType.AllBusinessUsers, null, null, null, [], [], [], _utc, CancellationToken.None)).Total.Should().Be(2);
    }

    [Fact]
    public async Task SelectedRecipients_RejectForeignTenantIdsAndDeduplicateUsers()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var local = Member(_tenant, userId, "M-1");
        var duplicate = Member(_tenant, userId, "M-2");
        var foreign = Member(_otherTenant, Guid.NewGuid(), "M-9");
        db.MemberProfiles.AddRange(local, duplicate, foreign);
        await db.SaveChangesAsync();

        var preview = await new CommunicationDelivery(db).PreviewAsync(
            _tenant,
            CommunicationAudienceType.SelectedMembers,
            null,
            null,
            null,
            [local.Id, duplicate.Id, foreign.Id],
            [],
            [],
            _utc,
            CancellationToken.None);

        preview.Total.Should().Be(1);
    }

    [Fact]
    public async Task Search_FiltersByAudienceAndPublishDate_AndPaginates()
    {
        await using var db = CreateDb();
        var owner = User(Guid.NewGuid());
        db.Users.Add(owner);
        var early = Communication.Create(Guid.NewGuid(), _tenant, owner.Id, CommunicationType.Announcement, "Early", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, _utc.AddDays(-2), null);
        var later = Communication.Create(Guid.NewGuid(), _tenant, owner.Id, CommunicationType.Offer, "Later", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllTrainers, null, null, null, _utc, null);
        early.Publish(_utc);
        later.Publish(_utc);
        db.Communications.AddRange(early, later);
        await db.SaveChangesAsync();

        var repository = new CommunicationRepository(db);
        var trainers = await repository.SearchAsync(_tenant, new CommunicationSearch(null, null, null, 1, 10, CommunicationAudienceType.AllTrainers, null, null), CancellationToken.None);
        trainers.Items.Should().ContainSingle().Which.Title.Should().Be("Later");
        var dated = await repository.SearchAsync(_tenant, new CommunicationSearch(null, null, null, 1, 1, null, _utc.AddDays(-1), _utc.AddDays(1)), CancellationToken.None);
        dated.TotalCount.Should().Be(1);
        dated.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Publish_SnapshotsRecipients_AndASecondDeliveryDoesNotDuplicate()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.MemberProfiles.Add(Member(_tenant, userId, "M-1"));
        var communication = Published(CommunicationAudienceType.AllMembers);
        db.Communications.Add(communication);
        await db.SaveChangesAsync();

        var delivery = new CommunicationDelivery(db);
        await delivery.DeliverAsync(communication, _utc, CancellationToken.None);
        await delivery.DeliverAsync(communication, _utc, CancellationToken.None);
        await db.SaveChangesAsync();

        var recipients = await db.CommunicationRecipients.Where(item => item.CommunicationId == communication.Id).ToListAsync();
        recipients.Should().ContainSingle();
        recipients[0].UserId.Should().Be(userId);
        recipients[0].ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Inbox_ShowsOnlyTheRecipient_HidesExpiredAndUnpublished_AndCountsUnread()
    {
        await using var db = CreateDb();
        var reader = Guid.NewGuid();
        var other = Guid.NewGuid();
        var visible = Published(CommunicationAudienceType.AllMembers);
        var expired = Published(CommunicationAudienceType.AllMembers, _utc.AddHours(-1));
        var scheduled = Communication.Create(Guid.NewGuid(), _tenant, reader, CommunicationType.Alert, "Later", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, _utc.AddDays(1), null);
        scheduled.Publish(_utc);
        db.Communications.AddRange(visible, expired, scheduled);
        db.CommunicationRecipients.Add(CommunicationRecipient.Create(Guid.NewGuid(), _tenant, visible.Id, reader, CommunicationRecipientKind.Member, null, _utc));
        db.CommunicationRecipients.Add(CommunicationRecipient.Create(Guid.NewGuid(), _tenant, visible.Id, other, CommunicationRecipientKind.Member, null, _utc));
        db.CommunicationRecipients.Add(CommunicationRecipient.Create(Guid.NewGuid(), _tenant, expired.Id, reader, CommunicationRecipientKind.Member, null, _utc));
        await db.SaveChangesAsync();

        var repository = new CommunicationRepository(db);
        var inbox = await repository.ListInboxAsync(_tenant, reader, null, false, 1, 20, _utc, CancellationToken.None);
        inbox.Items.Should().ContainSingle().Which.Id.Should().Be(visible.Id);
        (await repository.CountUnreadAsync(_tenant, reader, _utc, CancellationToken.None)).Should().Be(1);
        (await repository.ListInboxAsync(_tenant, other, null, false, 1, 20, _utc, CancellationToken.None)).Items.Should().ContainSingle();
        (await repository.ListInboxAsync(_otherTenant, reader, null, false, 1, 20, _utc, CancellationToken.None)).Items.Should().BeEmpty();

        var recipient = await repository.GetRecipientAsync(_tenant, reader, visible.Id, CancellationToken.None);
        recipient!.MarkRead(_utc);
        await db.SaveChangesAsync();
        (await repository.CountUnreadAsync(_tenant, reader, _utc, CancellationToken.None)).Should().Be(0);
        (await repository.ReadInboxAsync(_tenant, reader, visible.Id, _utc, CancellationToken.None))!.IsRead.Should().BeTrue();
        (await db.CommunicationRecipients.CountAsync(item => item.CommunicationId == expired.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ScheduledCommunication_IsDeliveredOnlyWhenDue()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.MemberProfiles.Add(Member(_tenant, userId, "M-1"));
        var communication = Communication.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), CommunicationType.Announcement, "Soon", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, _utc.AddHours(2), null);
        communication.Publish(_utc);
        db.Communications.Add(communication);
        await db.SaveChangesAsync();

        var delivery = new CommunicationDelivery(db);
        var repository = new CommunicationRepository(db);
        (await delivery.PublishDueAsync(_utc, CancellationToken.None)).Should().Be(0);
        (await repository.ListInboxAsync(_tenant, userId, null, false, 1, 20, _utc, CancellationToken.None)).Items.Should().BeEmpty();

        (await delivery.PublishDueAsync(_utc.AddHours(3), CancellationToken.None)).Should().Be(1);
        await db.SaveChangesAsync();
        communication.Status.Should().Be(CommunicationStatus.Published);
        (await repository.ListInboxAsync(_tenant, userId, null, false, 1, 20, _utc.AddHours(3), CancellationToken.None)).Items.Should().ContainSingle();
    }

    private MemberProfile Member(Guid tenantId, Guid userId, string code) =>
        MemberProfile.Create(Guid.NewGuid(), tenantId, userId, code, DateOnly.FromDateTime(_utc), "Ada", "Member");

    private UserAccount User(Guid id) =>
        UserAccount.Create(id, Guid.NewGuid(), $"{id:N}@example.com", null, "hash", "salt", "Ada", "User");

    private Communication Published(CommunicationAudienceType audience, DateTime? expiresAt = null)
    {
        var communication = Communication.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), CommunicationType.Announcement, "Notice", "Body", CommunicationPriority.Normal, audience, null, null, null, null, expiresAt);
        communication.Publish(_utc);
        return communication;
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
