using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class CommunicationFoundationTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Draft_CanPublishNowOrSchedule()
    {
        var communication = Communication.Create(Guid.NewGuid(), _tenantA, _userId, CommunicationType.Announcement, "Notice", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, null, null);
        communication.Publish(_utc);
        communication.Status.Should().Be(CommunicationStatus.Published);

        var scheduled = Communication.Create(Guid.NewGuid(), _tenantA, _userId, CommunicationType.Offer, "Sale", "Body", CommunicationPriority.High, CommunicationAudienceType.ActiveMembers, null, null, null, _utc.AddDays(2), null);
        scheduled.Publish(_utc);
        scheduled.Status.Should().Be(CommunicationStatus.Scheduled);
    }

    [Fact]
    public void Archived_CannotBeEditedOrPublished()
    {
        var communication = Communication.Create(Guid.NewGuid(), _tenantA, _userId, CommunicationType.Alert, "Alert", "Body", CommunicationPriority.High, CommunicationAudienceType.AllMembers, null, null, null, null, null);
        communication.Publish(_utc);
        communication.Archive(_utc);
        communication.CanEdit.Should().BeFalse();
        var act = () => communication.Publish(_utc);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_RequiresPermissionAndFeature()
    {
        var handler = Handler(GymErrors.FeatureDisabled);
        var result = await handler.Handle(new CreateCommunicationCommand(CommunicationType.General, "T", "M", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, null, null, null, false), CancellationToken.None);
        result.Error.Should().Be(GymErrors.FeatureDisabled);
    }

    [Fact]
    public async Task Inbox_RequiresPermissionAndFeature()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Failure(GymErrors.FeatureDisabled));
        var handler = new CommunicationInboxHandler(
            guard,
            Substitute.For<ICurrentActor>(),
            Substitute.For<ICommunicationRepository>(),
            Substitute.For<ICommunicationDelivery>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IDateTimeProvider>());
        var result = await handler.Handle(new CountUnreadCommunicationsQuery(), CancellationToken.None);
        result.Error.Should().Be(GymErrors.FeatureDisabled);
    }

    [Fact]
    public async Task Create_AllMembers_SucceedsWithoutSelectedList()
    {
        var communications = Substitute.For<ICommunicationRepository>();
        communications.ReadAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => new CommunicationDetails(call.ArgAt<Guid>(1), CommunicationType.Announcement, "Title", "Message", CommunicationPriority.Normal, CommunicationStatus.Draft, CommunicationAudienceType.AllMembers, null, null, null, [], null, null, null, _userId, "Owner", _utc, null, null));
        var handler = Handler(null, communications);
        var result = await handler.Handle(new CreateCommunicationCommand(CommunicationType.Announcement, "Title", "Message", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, null, null, null, false), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        await communications.Received(1).AddAsync(Arg.Any<Communication>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_RejectsExpiryBeforePublish()
    {
        var handler = Handler(null);
        var result = await handler.Handle(new CreateCommunicationCommand(CommunicationType.Announcement, "Title", "Message", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, null, _utc, _utc.AddHours(-1), false), CancellationToken.None);
        result.Error.Should().Be(GymErrors.InvalidState);
    }

    [Fact]
    public async Task Update_RejectsPublishedCommunication()
    {
        var communications = Substitute.For<ICommunicationRepository>();
        var published = Communication.Create(Guid.NewGuid(), _tenantA, _userId, CommunicationType.General, "Title", "Message", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, null, null);
        published.Publish(_utc);
        communications.GetAsync(_tenantA, published.Id, Arg.Any<CancellationToken>()).Returns(published);
        var handler = Handler(null, communications);
        var result = await handler.Handle(new UpdateCommunicationCommand(published.Id, CommunicationType.General, "Title", "Edited", CommunicationPriority.Normal, CommunicationAudienceType.AllMembers, null, null, null, null, null, null), CancellationToken.None);
        result.Error.Should().Be(GymErrors.InvalidState);
    }

    [Fact]
    public async Task SelectedMembers_RejectOutOfTenantMembers()
    {
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);
        var handler = Handler(null, people: people);
        var result = await handler.Handle(new CreateCommunicationCommand(CommunicationType.Circular, "Title", "Message", CommunicationPriority.Normal, CommunicationAudienceType.SelectedMembers, null, null, null, [Guid.NewGuid()], null, null, false), CancellationToken.None);
        result.Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public async Task MemberFeed_HidesOtherTenantCommunications()
    {
        var communications = Substitute.For<ICommunicationRepository>();
        communications.ListForMemberAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<Guid>(), null, 1, 20, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new PageResult<MemberCommunicationItem>([], 1, 20, 0));
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberByUserAsync(_tenantA, _userId, Arg.Any<CancellationToken>())
            .Returns(MemberProfile.Create(Guid.NewGuid(), _tenantA, _userId, "M1", DateOnly.FromDateTime(_utc)));
        var queries = QueryHandler(communications, people);
        var result = await queries.Handle(new ListMemberCommunicationsQuery(null, 1, 20), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        await communications.Received(1).ListForMemberAsync(_tenantA, Arg.Any<Guid>(), _userId, null, 1, 20, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await communications.DidNotReceive().ListForMemberAsync(_tenantB, Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CommunicationType?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkRead_RejectsUnmatchedAudience()
    {
        var communication = Communication.Create(Guid.NewGuid(), _tenantA, _userId, CommunicationType.Offer, "Sale", "Body", CommunicationPriority.Normal, CommunicationAudienceType.SelectedMembers, null, null, null, _utc.AddMinutes(-1), null);
        communication.Publish(_utc);
        var communications = Substitute.For<ICommunicationRepository>();
        communications.GetAsync(_tenantA, communication.Id, Arg.Any<CancellationToken>()).Returns(communication);
        communications.IsAudienceMatchAsync(_tenantA, communication, Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(false);
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberByUserAsync(_tenantA, _userId, Arg.Any<CancellationToken>())
            .Returns(MemberProfile.Create(Guid.NewGuid(), _tenantA, _userId, "M1", DateOnly.FromDateTime(_utc)));
        var result = await QueryHandler(communications, people).Handle(new MarkCommunicationReadCommand(communication.Id), CancellationToken.None);
        result.Error.Should().Be(GymErrors.NotFound);
    }

    private CommunicationCommandHandler Handler(Error? error, ICommunicationRepository? communications = null, IPeopleRepository? people = null)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(error is null ? Result.Success() : Result.Failure(error));
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenantA);
        actor.UserId.Returns(_userId);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        var schedule = Substitute.For<ICommunicationSchedule>();
        schedule.NormalizeAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Success(new CommunicationScheduleTimes(call.ArgAt<DateTime?>(1), call.ArgAt<DateTime?>(2), "UTC")));
        return new CommunicationCommandHandler(
            guard,
            actor,
            communications ?? Substitute.For<ICommunicationRepository>(),
            Substitute.For<ICommunicationDelivery>(),
            people ?? Substitute.For<IPeopleRepository>(),
            Substitute.For<IMembershipRepository>(),
            Substitute.For<IAuditLogRepository>(),
            unit,
            clock,
            Substitute.For<IClientContext>(),
            schedule);
    }

    private CommunicationQueryHandler QueryHandler(ICommunicationRepository communications, IPeopleRepository people)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenantA);
        actor.UserId.Returns(_userId);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return new CommunicationQueryHandler(guard, actor, communications, people, Substitute.For<IUnitOfWork>(), clock);
    }
}
