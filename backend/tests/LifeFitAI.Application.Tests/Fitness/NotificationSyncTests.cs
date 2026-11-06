using FluentAssertions;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Fitness.Challenges;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Fitness;

public sealed class NotificationSyncTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _user = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Summary_RequiresPermissionAndFeature()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Failure(GymErrors.FeatureDisabled));
        var handler = new NotificationHandler(guard, Actor(), Substitute.For<IChallengeRepository>(), Substitute.For<IUnitOfWork>(), Clock());
        var result = await handler.Handle(new GetNotificationSummaryQuery(), CancellationToken.None);
        result.Error.Should().Be(GymErrors.FeatureDisabled);
    }

    [Fact]
    public async Task Summary_ReturnsUnreadAndServerTime()
    {
        var challenges = Substitute.For<IChallengeRepository>();
        challenges.CountUnreadNotificationsAsync(_tenant, _user, Arg.Any<CancellationToken>()).Returns(3);
        challenges.LatestNotificationActivityAsync(_tenant, _user, Arg.Any<CancellationToken>()).Returns(_utc.AddMinutes(-5));
        var handler = new NotificationHandler(Allow(), Actor(), challenges, Substitute.For<IUnitOfWork>(), Clock());
        var result = await handler.Handle(new GetNotificationSummaryQuery(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadCount.Should().Be(3);
        result.Value.LatestActivityAt.Should().Be(_utc.AddMinutes(-5));
        result.Value.ServerTimeUtc.Should().Be(_utc);
    }

    [Fact]
    public async Task Sync_ReturnsIncrementalItemsForCurrentTenantUser()
    {
        var note = Notification.Create(Guid.NewGuid(), _user, _tenant, NotificationType.ChallengeReminder, "Title", "Body", _utc);
        var challenges = Substitute.For<IChallengeRepository>();
        challenges.SyncNotificationsAsync(_tenant, _user, _utc.AddHours(-1), 50, Arg.Any<CancellationToken>()).Returns([note]);
        challenges.CountUnreadNotificationsAsync(_tenant, _user, Arg.Any<CancellationToken>()).Returns(1);
        var handler = new NotificationHandler(Allow(), Actor(), challenges, Substitute.For<IUnitOfWork>(), Clock());
        var result = await handler.Handle(new SyncNotificationsQuery(_utc.AddHours(-1), 50), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(item => item.Id == note.Id);
        result.Value.UnreadCount.Should().Be(1);
        result.Value.ServerTimeUtc.Should().Be(_utc);
    }

    private IOperationGuard Allow()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.NotificationRead, FeatureCodes.WorkoutPlans, Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenant);
        actor.UserId.Returns(_user);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return clock;
    }
}
