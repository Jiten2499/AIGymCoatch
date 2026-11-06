using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
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

public sealed class TrainerManagementTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _other = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _actor = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateOnly _today = new(2026, 10, 9);

    [Fact]
    public void Incentive_IsCalculatedFromActualRevenueOnlyWhenTheTargetIsMet()
    {
        TrainerPerformanceMath.IncentiveAmount(100000m, 100000m, 5m).Should().Be(5000m);
        TrainerPerformanceMath.IncentiveAmount(0m, 100000m, 5m).Should().BeNull();
        TrainerPerformanceMath.PeriodRange(TrainerTargetPeriod.Quarterly, new DateOnly(2026, 11, 2), new DateOnly(2026, 1, 1)).Start.Should().Be(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public async Task Profile_ReusesTheExistingUser_AndIsolatesTenants()
    {
        await using var db = CreateDb();
        var user = User(_tenant);
        db.Users.Add(user);
        db.Tenants.Add(Tenant.Create(_tenant, "A", TenantType.Gym));
        db.Tenants.Add(Tenant.Create(_other, "B", TenantType.Gym));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, user.Id, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var workspace = new TrainerWorkspace(db);
        var created = await workspace.CreateFromUserAsync(_tenant, user.Id, "T-1", "Strength", "Bio", _today, true, CancellationToken.None);
        await db.SaveChangesAsync();
        var duplicate = await workspace.CreateFromUserAsync(_tenant, user.Id, "T-2", null, null, _today, false, CancellationToken.None);

        created.Should().NotBeNull();
        duplicate.Should().BeNull();
        (await db.Users.CountAsync()).Should().Be(1);
        (await workspace.ListAsync(_other, CancellationToken.None)).Should().BeEmpty();
        (await workspace.ListAsync(_tenant, CancellationToken.None)).Should().ContainSingle().Which.Specialization.Should().Be("Strength");
    }

    [Fact]
    public async Task Reassignment_KeepsHistory_AndPerformanceUsesAssignments()
    {
        await using var db = CreateDb();
        var workspace = new TrainerWorkspace(db);
        var firstUser = User(_tenant);
        var secondUser = User(_other);
        db.Users.AddRange(firstUser, secondUser);
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, firstUser.Id, DateTime.UtcNow));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, secondUser.Id, DateTime.UtcNow));
        var member = MemberProfile.Create(Guid.NewGuid(), _tenant, null, "M-1", _today, "Ana", "Lee");
        db.MemberProfiles.Add(member);
        await db.SaveChangesAsync();
        var first = (await workspace.CreateFromUserAsync(_tenant, firstUser.Id, null, null, null, _today, false, CancellationToken.None))!;
        var second = (await workspace.CreateFromUserAsync(_tenant, secondUser.Id, null, null, null, _today, false, CancellationToken.None))!;
        await db.SaveChangesAsync();
        var assignment = await workspace.AssignAsync(_tenant, first.Id, member.Id, _today, _actor, CancellationToken.None);
        await db.SaveChangesAsync();
        await workspace.ChangeTrainerAsync(_tenant, assignment!.Id, second.Id, _today.AddDays(10), _actor, CancellationToken.None);
        await workspace.AddTargetAsync(_tenant, second.Id, TrainerTargetType.NewPtClients, TrainerTargetPeriod.Monthly, _today, _today, 1m, CancellationToken.None);
        await workspace.SaveIncentiveRuleAsync(_tenant, TrainerTargetType.PtRevenue, 5m, CancellationToken.None);
        await db.SaveChangesAsync();

        var history = await db.TrainerMemberAssignments.OrderBy(item => item.StartDate).ToListAsync();
        history.Should().HaveCount(2);
        history[0].Status.Should().Be(TrainerAssignmentStatus.Ended);
        history[1].TrainerProfileId.Should().Be(second.Id);
        (await db.MemberProfiles.SingleAsync()).AssignedTrainerId.Should().Be(second.Id);
        var performance = await workspace.PerformanceAsync(_tenant, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None);
        performance.Single(item => item.TrainerId == second.Id).NewPtClients.Should().Be(1);
        performance.Single(item => item.TrainerId == second.Id).PtRevenue.Should().BeNull();
        performance.Single(item => item.TrainerId == second.Id).TargetAchievementPercent.Should().Be(100);
        var details = await workspace.DetailsAsync(_tenant, second.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None);
        details!.Incentives.Should().ContainSingle().Which.Status.Should().Be("No target");
    }

    [Fact]
    public void Compensation_DoesNotInventUnavailableRevenue()
    {
        CoachCompensationMath.Commission(CommissionRuleKind.PercentOfEligiblePtRevenue, 5m, null, null, null).Should().BeNull();
        CoachCompensationMath.Incentive(IncentiveRewardKind.FixedOnAchievement, 2m, 1m, 500m, null, null).Should().Be(500m);
        CoachCompensationMath.Incentive(IncentiveRewardKind.PercentOfEligibleRevenue, 2m, 1m, null, 5m, null).Should().BeNull();
    }

    [Fact]
    public async Task Incentive_IsCalculatedOnce_AndApprovalDoesNotMarkItPaid()
    {
        await using var db = CreateDb();
        var workspace = new TrainerWorkspace(db);
        var user = User(_tenant);
        db.Users.Add(user);
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, user.Id, DateTime.UtcNow));
        var member = MemberProfile.Create(Guid.NewGuid(), _tenant, null, "M-9", _today, "Nia", "Cole");
        db.MemberProfiles.Add(member);
        await db.SaveChangesAsync();
        var trainer = (await workspace.CreateFromUserAsync(_tenant, user.Id, "E-9", null, null, _today, false, CancellationToken.None))!;
        await db.SaveChangesAsync();
        await workspace.AssignAsync(_tenant, trainer.Id, member.Id, _today, _actor, CancellationToken.None);
        await workspace.AddPerformanceIncentiveAsync(_tenant, trainer.Id, "New clients", TrainerTargetType.NewPtClients, 1m, IncentiveRewardKind.FixedOnAchievement, 500m, null, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), true, CancellationToken.None);
        await db.SaveChangesAsync();
        var first = await workspace.CalculateCompensationAsync(_tenant, trainer.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None);
        var second = await workspace.CalculateCompensationAsync(_tenant, trainer.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None);
        await db.SaveChangesAsync();
        var row = await db.TrainerCompensationCalculations.SingleAsync();
        var approved = await workspace.DecideCalculationAsync(_tenant, row.Id, CompensationStatus.Approved, CancellationToken.None);

        first.Should().Be(1);
        second.Should().Be(0);
        approved!.Status.Should().Be(CompensationStatus.Approved);
        (await workspace.CoachAsync(_other, trainer.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task UpdateMember_RecordsTheAssignedTrainer()
    {
        await using var db = CreateDb();
        var workspace = new TrainerWorkspace(db);
        var user = User(_tenant);
        db.Users.Add(user);
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, user.Id, DateTime.UtcNow));
        var member = MemberProfile.Create(Guid.NewGuid(), _tenant, null, "M-2", _today, "Bea", "Cole");
        db.MemberProfiles.Add(member);
        await db.SaveChangesAsync();
        var trainer = (await workspace.CreateFromUserAsync(_tenant, user.Id, null, null, null, _today, false, CancellationToken.None))!;
        await db.SaveChangesAsync();
        var people = new PeopleRepository(db);
        var handler = new UpdateMemberCommandHandler(Allow(), Actor(), people, Substitute.For<IUserAccountRepository>(), Substitute.For<IAuditLogRepository>(), new Unit(db), Clock(), Substitute.For<IClientContext>(), workspace);
        var result = await handler.Handle(new UpdateMemberCommand(member.Id, "Bea", "Cole", null, null, null, null, null, null, null, null, null, null, null, null, trainer.Id, ProfileStatus.Active), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.TrainerMemberAssignments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Authorization_AndDisabledFeature_BlockManagement()
    {
        var workspace = Substitute.For<ITrainerWorkspace>();
        var denied = Handler(workspace, Result.Failure(IdentityErrors.Forbidden));
        (await denied.Handle(new ListTrainerWorkspaceQuery(), CancellationToken.None)).Error.Should().Be(IdentityErrors.Forbidden);
        var disabled = Handler(workspace, Result.Failure(GymErrors.FeatureDisabled));
        (await disabled.Handle(new CreateTrainerProfileCommand(Guid.NewGuid(), null, null, null, null, false), CancellationToken.None)).Error.Should().Be(GymErrors.FeatureDisabled);
        await workspace.DidNotReceiveWithAnyArgs().CreateFromUserAsync(default, default, default, default, default, default, default, default);
    }

    private TrainerWorkspaceHandlers Handler(ITrainerWorkspace workspace, Result gate)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(gate);
        return new TrainerWorkspaceHandlers(guard, Actor(), workspace, Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());
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
        actor.TenantId.Returns(_tenant);
        actor.UserId.Returns(_actor);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc));
        return clock;
    }

    private static UserAccount User(Guid tenantId) =>
        UserAccount.Create(Guid.NewGuid(), tenantId, $"{Guid.NewGuid():N}@example.com", null, "hash", "salt", "Ada", "Trainer");

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new RowVersionInterceptor())
            .Options;
        return new ApplicationDbContext(options);
    }

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
