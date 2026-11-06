using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class TrainerRewardTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _other = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _actor = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateOnly _today = new(2026, 10, 20);

    [Fact]
    public void Balance_SumsApprovedLedgerRowsOnly()
    {
        var award = TrainerRewardTransaction.Award(Guid.NewGuid(), _tenant, Guid.NewGuid(), 10, Guid.NewGuid(), "a1", null, false, DateTime.UtcNow, _actor)!;
        var pending = TrainerRewardTransaction.Award(Guid.NewGuid(), _tenant, award.TrainerProfileId, 7, Guid.NewGuid(), "a2", null, true, DateTime.UtcNow, _actor)!;
        var reversal = TrainerRewardTransaction.Reverse(Guid.NewGuid(), award, "r1", "undo", DateTime.UtcNow, _actor)!;
        TrainerRewardBalance.From([award, pending, reversal]).Should().Be(0);
        TrainerRewardBalance.From([award]).Should().Be(10);
    }

    [Fact]
    public async Task Award_IsIdempotent_AndBlocksDuplicateOpenAwards()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, requireApproval: false);
        var assignment = await AssignAsync(harness);
        var first = await harness.Ledger.Handle(new AwardTrainerRewardCommand(assignment.Id, null, "award-1", null), CancellationToken.None);
        var again = await harness.Ledger.Handle(new AwardTrainerRewardCommand(assignment.Id, null, "award-1", null), CancellationToken.None);
        var duplicate = await harness.Ledger.Handle(new AwardTrainerRewardCommand(assignment.Id, 5, "award-2", null), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        again.Value.Id.Should().Be(first.Value.Id);
        duplicate.Error.Code.Should().Be(GymErrors.InvalidState.Code);
        (await harness.Store.BalanceAsync(_tenant, harness.TrainerId, CancellationToken.None)).Should().Be(10);
        (await db.TrainerTargetAssignments.SingleAsync(item => item.Id == assignment.Id)).Status.Should().Be(TrainerTargetAssignmentStatus.Completed);
    }

    [Fact]
    public async Task Assign_AllowsNewTarget_WhenPreviousSameDefinitionIsCompleted()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, requireApproval: false);
        var first = await AssignAsync(harness);
        var award = await harness.Ledger.Handle(new AwardTrainerRewardCommand(first.Id, null, "award-done", null), CancellationToken.None);
        award.IsSuccess.Should().BeTrue();

        var second = await harness.Commands.Handle(
            new AssignTrainerTargetCommand(harness.TrainerId, first.TargetDefinitionId, 60m, _today, _today.AddDays(19)),
            CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        second.Value.Id.Should().NotBe(first.Id);
        second.Value.GoalValue.Should().Be(60m);
        (await db.TrainerTargetAssignments.CountAsync(item => item.TrainerProfileId == harness.TrainerId && item.TargetDefinitionId == first.TargetDefinitionId))
            .Should().Be(2);
    }

    [Fact]
    public async Task Reversal_PostsNegativeApprovedPoints()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, requireApproval: false);
        var assignment = await AssignAsync(harness);
        var award = await harness.Ledger.Handle(new AwardTrainerRewardCommand(assignment.Id, 25, "award-r", null), CancellationToken.None);
        var reverse = await harness.Ledger.Handle(new ReverseTrainerRewardCommand(award.Value.Id, "rev-1", "correction"), CancellationToken.None);

        reverse.IsSuccess.Should().BeTrue();
        reverse.Value.Points.Should().Be(-25);
        reverse.Value.ApprovalStatus.Should().Be(nameof(TrainerRewardApprovalStatus.Approved));
        (await harness.Store.BalanceAsync(_tenant, harness.TrainerId, CancellationToken.None)).Should().Be(0);
    }

    [Fact]
    public async Task Approval_PostsPendingAward_AndKeepsPendingOutOfBalance()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, requireApproval: true);
        var assignment = await AssignAsync(harness);
        var award = await harness.Ledger.Handle(new AwardTrainerRewardCommand(assignment.Id, 15, "pending-1", null), CancellationToken.None);
        award.Value.ApprovalStatus.Should().Be(nameof(TrainerRewardApprovalStatus.Pending));
        (await harness.Store.BalanceAsync(_tenant, harness.TrainerId, CancellationToken.None)).Should().Be(0);

        var approval = await db.ApprovalRequests.SingleAsync();
        var decided = await harness.Decide.Handle(new DecideApprovalRequestCommand(approval.Id, true, null), CancellationToken.None);
        decided.IsSuccess.Should().BeTrue();
        (await harness.Store.BalanceAsync(_tenant, harness.TrainerId, CancellationToken.None)).Should().Be(15);
    }

    [Fact]
    public async Task RejectAward_CancelsAssignment_AndAllowsNewAssign()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, requireApproval: true);
        var first = await AssignAsync(harness);
        var award = await harness.Ledger.Handle(new AwardTrainerRewardCommand(first.Id, 15, "pending-reject", null), CancellationToken.None);
        award.IsSuccess.Should().BeTrue();

        var approval = await db.ApprovalRequests.SingleAsync();
        var rejected = await harness.Decide.Handle(new DecideApprovalRequestCommand(approval.Id, false, "Not earned"), CancellationToken.None);
        rejected.IsSuccess.Should().BeTrue();
        (await db.TrainerTargetAssignments.SingleAsync(item => item.Id == first.Id)).Status.Should().Be(TrainerTargetAssignmentStatus.Active);
        var again = await harness.Ledger.Handle(new AwardTrainerRewardCommand(first.Id, 15, "pending-reject-2", null), CancellationToken.None);
        again.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TenantIsolation_HidesOtherTenantDefinitionsAndBalances()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, requireApproval: false);
        await AssignAsync(harness);
        harness.Actor.TenantId.Returns(_other);
        var definitions = await harness.Queries.Handle(new ListTrainerTargetDefinitionsQuery(), CancellationToken.None);
        var balance = await harness.Queries.Handle(new GetTrainerRewardBalanceQuery(harness.TrainerId), CancellationToken.None);

        definitions.Value.Should().BeEmpty();
        balance.Error.Code.Should().Be(GymErrors.NotFound.Code);
    }

    private async Task<TrainerTargetAssignmentItem> AssignAsync(Harness harness)
    {
        var created = await harness.Commands.Handle(
            new CreateTrainerTargetDefinitionCommand("New clients", null, TrainerTargetType.NewPtClients, 1m, TrainerTargetPeriod.Monthly, 10, "Active"),
            CancellationToken.None);
        created.IsSuccess.Should().BeTrue();
        var assigned = await harness.Commands.Handle(
            new AssignTrainerTargetCommand(harness.TrainerId, created.Value.Id, null, _today, null),
            CancellationToken.None);
        assigned.IsSuccess.Should().BeTrue();
        return assigned.Value;
    }

    private async Task<Harness> SeedAsync(ApplicationDbContext db, bool requireApproval)
    {
        db.Tenants.Add(Tenant.Create(_tenant, "A", TenantType.Gym));
        db.Tenants.Add(Tenant.Create(_other, "B", TenantType.Gym));
        db.Users.Add(UserAccount.Create(_actor, _tenant, "coach@example.com", null, "hash", "salt", "Ada", "Coach"));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, _actor, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var workspace = new TrainerWorkspace(db);
        var trainer = await workspace.CreateFromUserAsync(_tenant, _actor, "T-1", "Strength", null, _today, true, CancellationToken.None);
        trainer.Should().NotBeNull();
        await db.SaveChangesAsync();

        var store = new TrainerRewardStore(db);
        var settings = await store.GetOrCreateSettingsAsync(_tenant, CancellationToken.None);
        settings.Update(true, 10, requireApproval, true);
        await db.SaveChangesAsync();

        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenant);
        actor.UserId.Returns(_actor);
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc));
        var client = Substitute.For<IClientContext>();
        client.IpAddress.Returns("127.0.0.1");
        client.UserAgent.Returns("test");
        var unit = new Unit(db);
        var approvals = new ApprovalRequestRepository(db);
        var users = Substitute.For<IUserAccountRepository>();
        users.GetByIdAsync(_actor, Arg.Any<CancellationToken>()).Returns(await db.Users.SingleAsync(item => item.Id == _actor));
        var audit = Substitute.For<IAuditLogRepository>();
        audit.AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        return new Harness(
            store,
            actor,
            new TrainerRewardCommandHandlers(guard, actor, store, audit, unit, clock, client),
            new TrainerRewardLedgerHandlers(guard, actor, store, approvals, users, audit, unit, clock, client),
            new TrainerRewardQueryHandlers(guard, actor, store, unit),
            new DecideApprovalRequestCommandHandler(guard, actor, permissions, approvals, Substitute.For<IMembershipRepository>(), Substitute.For<IBusinessConfigRepository>(), store, Substitute.For<LifeFitAI.Application.Gym.Leave.ILeaveStore>(), Substitute.For<LifeFitAI.Application.Fitness.IChallengeRepository>(), users, audit, unit, clock, client),
            trainer!.Id);
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

    private sealed record Harness(
        ITrainerRewardStore Store,
        ICurrentActor Actor,
        TrainerRewardCommandHandlers Commands,
        TrainerRewardLedgerHandlers Ledger,
        TrainerRewardQueryHandlers Queries,
        DecideApprovalRequestCommandHandler Decide,
        Guid TrainerId);

    private sealed class Unit : IUnitOfWork
    {
        private readonly ApplicationDbContext _db;
        public Unit(ApplicationDbContext db) => _db = db;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) => action(cancellationToken);
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
