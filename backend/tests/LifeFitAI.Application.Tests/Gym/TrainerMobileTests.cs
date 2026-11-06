using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class TrainerMobileTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _other = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _actor = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateOnly _today = new(2026, 10, 23);

    [Fact]
    public async Task Schedule_RejectsMemberWhoIsNotAssigned()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db);
        var stranger = MemberProfile.Create(Guid.NewGuid(), _tenant, null, "M-2", _today, "Neha", "Gupta", null);
        db.MemberProfiles.Add(stranger);
        await db.SaveChangesAsync();

        var result = await harness.Commands.Handle(new ScheduleMyTrainerSessionCommand(stranger.Id, At(10), At(11), "Strength", "Gym Floor", null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(GymErrors.NotFound.Code);
        (await db.PtSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Complete_ConsumesOnePackage_AndSecondCallDoesNotAwardCredits()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db);
        var scheduled = await harness.Commands.Handle(new ScheduleMyTrainerSessionCommand(harness.MemberId, At(10), At(11), "Strength", "Gym Floor", "Form"), CancellationToken.None);
        scheduled.IsSuccess.Should().BeTrue();

        var first = await harness.Commands.Handle(new CompleteMyTrainerSessionCommand(scheduled.Value, "Focused on form.", 3600), CancellationToken.None);
        var second = await harness.Commands.Handle(new CompleteMyTrainerSessionCommand(scheduled.Value, "Again", 3600), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.Value.Should().Be(first.Value);
        (await db.PtSessionConsumptions.CountAsync()).Should().Be(1);
        (await db.MemberPtAssignments.SingleAsync()).SessionsRemaining.Should().Be(11);
        (await db.PtSessions.SingleAsync()).Status.Should().Be(PtSessionStatus.Completed);
        (await db.TrainerRewardTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TenantIsolation_HidesAnotherTenantsSession()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db);
        var foreignTrainer = Guid.NewGuid();
        var foreignMember = Guid.NewGuid();
        db.TrainerProfiles.Add(TrainerProfile.Create(foreignTrainer, _other, Guid.NewGuid(), null));
        db.MemberProfiles.Add(MemberProfile.Create(foreignMember, _other, null, "M-9", _today, "Other", "Member", null));
        var foreign = PtSession.Schedule(Guid.NewGuid(), _other, foreignMember, foreignTrainer, At(9), At(10), "PT", null, null);
        db.PtSessions.Add(foreign!);
        await db.SaveChangesAsync();

        var hidden = await harness.Queries.Handle(new GetMyTrainerSessionQuery(foreign!.Id), CancellationToken.None);
        harness.Actor.TenantId.Returns(_other);
        var denied = await harness.Queries.Handle(new GetTrainerHomeQuery(), CancellationToken.None);

        hidden.IsSuccess.Should().BeFalse();
        hidden.Error.Code.Should().Be(GymErrors.NotFound.Code);
        denied.IsSuccess.Should().BeFalse();
        (await db.PtSessions.CountAsync(item => item.TenantId == _tenant)).Should().Be(0);
    }

    private DateTime At(int hour) => new(_today.Year, _today.Month, _today.Day, hour, 0, 0, DateTimeKind.Utc);

    private async Task<Harness> SeedAsync(ApplicationDbContext db)
    {
        db.Tenants.Add(Tenant.Create(_tenant, "A", TenantType.Gym));
        db.Tenants.Add(Tenant.Create(_other, "B", TenantType.Gym));
        db.Users.Add(UserAccount.Create(_actor, _tenant, "coach@example.com", null, "hash", "salt", "Rahul", "Kumar"));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, _actor, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var trainer = await new TrainerWorkspace(db).CreateFromUserAsync(_tenant, _actor, "T-1", "Strength", null, _today, true, CancellationToken.None);
        var member = MemberProfile.Create(Guid.NewGuid(), _tenant, null, "M-1", _today, "Priya", "Sharma", null);
        member.Update(null, null, null, null, null, null, null, null, ProfileStatus.Active, assignedTrainerId: trainer!.Id);
        var package = PtPackage.Create(Guid.NewGuid(), _tenant, "Personal Training", null, 12, 90, 5000m, "INR");
        db.MemberProfiles.Add(member);
        db.PtPackages.Add(package);
        db.MemberPtAssignments.Add(MemberPtAssignment.Open(Guid.NewGuid(), package, member.Id, trainer.Id, _today));
        await db.SaveChangesAsync();

        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenant);
        actor.UserId.Returns(_actor);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(At(12));
        var store = new TrainerMobileStore(db);
        var unit = new Unit(db);
        return new Harness(actor, new TrainerMobileQueryHandlers(actor, store, clock), new TrainerMobileCommandHandlers(actor, store, unit, clock), member.Id);
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

    private sealed record Harness(ICurrentActor Actor, TrainerMobileQueryHandlers Queries, TrainerMobileCommandHandlers Commands, Guid MemberId);

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
