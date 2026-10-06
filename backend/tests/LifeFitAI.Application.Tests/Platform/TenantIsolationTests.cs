using FluentAssertions;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Fitness.Ai;
using LifeFitAI.Application.Fitness.Nutrition;
using LifeFitAI.Application.Fitness.Workout;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Platform;

public sealed class TenantIsolationTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private readonly Guid _userA = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private readonly Guid _foreignId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");

    [Fact]
    public async Task TenantA_CannotReadOrUpdateTenantBMember()
    {
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberAsync(_tenantA, _foreignId, Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);
        var read = new GetMemberQueryHandler(Allow(), Actor(), people, Substitute.For<IUserAccountRepository>());
        var update = new UpdateMemberCommandHandler(Allow(), Actor(), people, Substitute.For<IUserAccountRepository>(), Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

        var readResult = await read.Handle(new GetMemberQuery(_foreignId), CancellationToken.None);
        var updateResult = await update.Handle(new UpdateMemberCommand(_foreignId, null, null, null, null, null, null, null, null, ProfileStatus.Inactive), CancellationToken.None);

        readResult.IsFailure.Should().BeTrue();
        updateResult.IsFailure.Should().BeTrue();
        await people.Received(2).GetMemberAsync(_tenantA, _foreignId, Arg.Any<CancellationToken>());
        await people.DidNotReceive().GetMemberAsync(_tenantB, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TenantA_CannotReadTenantBAttendanceWorkoutNutritionOrConversation()
    {
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.ListAsync(_tenantA, _foreignId, _foreignId, null, null, Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PageResult<AttendanceResponse>([], 1, 20, 0));
        var workouts = Substitute.For<IWorkoutRepository>();
        var nutrition = Substitute.For<INutritionRepository>();
        nutrition.ListLogsAsync(_tenantA, _userA, null, Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PageResult<NutritionLog>([], 1, 20, 0));
        var ai = Substitute.For<IAiRepository>();

        var visits = await new ListAttendanceQueryHandler(Allow(), Actor(), attendance)
            .Handle(new ListAttendanceQuery(_foreignId, _foreignId, null, null, 1, 20), CancellationToken.None);
        var session = await new GetSessionQueryHandler(Allow(), Actor(), workouts)
            .Handle(new GetSessionQuery(_foreignId), CancellationToken.None);
        var logs = await new NutritionLogHandler(Allow(), Actor(), nutrition, Substitute.For<IUnitOfWork>(), Clock())
            .Handle(new ListNutritionLogsQuery(null, 1, 20), CancellationToken.None);
        var conversation = await new ConversationQueryHandler(Allow(), Actor(), ai)
            .Handle(new GetConversationQuery(_foreignId, 1, 20), CancellationToken.None);

        visits.IsSuccess.Should().BeTrue();
        session.IsFailure.Should().BeTrue();
        logs.IsSuccess.Should().BeTrue();
        conversation.IsFailure.Should().BeTrue();
        await attendance.Received().ListAsync(_tenantA, Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>());
        await attendance.DidNotReceive().ListAsync(_tenantB, Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>());
        await workouts.Received().GetSessionAsync(_tenantA, _userA, _foreignId, Arg.Any<CancellationToken>());
        await workouts.DidNotReceive().GetSessionAsync(_tenantB, Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await nutrition.Received().ListLogsAsync(_tenantA, _userA, null, Arg.Any<PageQuery>(), Arg.Any<CancellationToken>());
        await nutrition.DidNotReceive().ListLogsAsync(_tenantB, Arg.Any<Guid>(), Arg.Any<DateOnly?>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>());
        await ai.Received().GetConversationAsync(_tenantA, _userA, _foreignId, Arg.Any<CancellationToken>());
        await ai.DidNotReceive().GetConversationAsync(_tenantB, Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TenantA_CannotReadTenantBCacheOrInjectTenantBIntoAiContext()
    {
        var cache = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
        await cache.SetAsync(TenantCache.Status(_tenantB), "Active", TimeSpan.FromMinutes(1), CancellationToken.None);
        var leaked = await cache.GetAsync<string>(TenantCache.Status(_tenantA), CancellationToken.None);
        leaked.Should().BeNull();
        TenantCache.Permissions(_tenantA, _userA).Should().NotBe(TenantCache.Permissions(_tenantB, _userA));
        TenantCache.Features(_tenantA).Should().Contain(_tenantA.ToString("N"));

        var fitness = Substitute.For<IFitnessRepository>();
        var workouts = Substitute.For<IWorkoutRepository>();
        var nutrition = Substitute.For<INutritionRepository>();
        var challenges = Substitute.For<IChallengeRepository>();
        workouts.RecentWorkoutLabelsAsync(_tenantA, _userA, 5, Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        nutrition.SummarizeDayAsync(_tenantA, _userA, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new NutritionSummaryResponse(new DateOnly(2026, 10, 1), 0, 0, 0, 0, 0, 0, 0, 0));
        var builder = new AiContextBuilder(fitness, workouts, nutrition, challenges, Clock());

        var context = await builder.BuildAsync(_userA, _tenantA, CancellationToken.None);
        var prompt = FitnessPrompts.Coach(context);

        prompt.Should().NotContain("@");
        prompt.Should().NotContain(_tenantB.ToString());
        await fitness.Received().GetProfileAsync(_tenantA, _userA, Arg.Any<CancellationToken>());
        await fitness.DidNotReceive().GetProfileAsync(_tenantB, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuditEntriesStayOnTheActingTenant()
    {
        var audit = Substitute.For<IAuditLogRepository>();
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberAsync(_tenantA, _foreignId, Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);
        var handler = new UpdateMemberCommandHandler(Allow(), Actor(), people, Substitute.For<IUserAccountRepository>(), audit, Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());

        await handler.Handle(new UpdateMemberCommand(_foreignId, null, null, null, null, null, null, null, null, ProfileStatus.Inactive), CancellationToken.None);

        await audit.DidNotReceive().AddAsync(Arg.Is<AuditLog>(entry => entry.TenantId == _tenantB), Arg.Any<CancellationToken>());
    }

    private IOperationGuard Allow()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_userA);
        actor.TenantId.Returns(_tenantA);
        return actor;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        return clock;
    }
}
