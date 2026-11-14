using FluentAssertions;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Fitness.Workout;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class TrainerMemberProgramTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _actor = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly Guid _memberUser = Guid.Parse("dddddddd-dddd-4ddd-8ddd-ddddddddddd1");
    private readonly DateOnly _today = new(2026, 10, 23);

    [Fact]
    public async Task AssignedTrainer_CanListGetAndUpdateMemberProgram()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, assignTrainer: true);
        var exerciseId = Guid.NewGuid();
        db.Exercises.Add(Exercise.CreateCustom(exerciseId, _tenant, "Squat", null, null, MuscleGroup.Legs, "Barbell", ExerciseType.Strength, WorkoutDifficulty.Intermediate));
        var program = WorkoutProgram.Create(Guid.NewGuid(), _tenant, _memberUser, "AI Plan", "Month one", FitnessGoalType.Strength, WorkoutDifficulty.Intermediate, 4, 3);
        var day = WorkoutProgramDay.Create(Guid.NewGuid(), program.Id, 1, "Day 1", null, false);
        db.WorkoutPrograms.Add(program);
        db.WorkoutProgramDays.Add(day);
        db.WorkoutProgramExercises.Add(WorkoutProgramExercise.Create(Guid.NewGuid(), day.Id, exerciseId, 1, 3, 8, null, null, 60m, 90, "Pause"));
        await db.SaveChangesAsync();

        var listed = await harness.Handlers.Handle(new ListMemberProgramsQuery(harness.MemberId), CancellationToken.None);
        var got = await harness.Handlers.Handle(new GetMemberProgramQuery(harness.MemberId, program.Id), CancellationToken.None);
        var updated = await harness.Handlers.Handle(
            new UpdateMemberProgramCommand(
                harness.MemberId,
                program.Id,
                "Edited Plan",
                "Coach notes",
                FitnessGoalType.Strength,
                WorkoutDifficulty.Advanced,
                4,
                4,
                [
                    new ProgramDayInput(
                        1,
                        "Push",
                        null,
                        false,
                        [new ProgramExerciseInput(exerciseId, 4, 6, null, null, 80m, 120, "Slow eccentric")]),
                    new ProgramDayInput(2, "Rest", null, true, []),
                ]),
            CancellationToken.None);

        listed.IsSuccess.Should().BeTrue();
        listed.Value.Items.Should().ContainSingle(item => item.Id == program.Id);
        got.IsSuccess.Should().BeTrue();
        got.Value.Days.Should().ContainSingle();
        updated.IsSuccess.Should().BeTrue();
        updated.Value.Name.Should().Be("Edited Plan");
        updated.Value.Difficulty.Should().Be(WorkoutDifficulty.Advanced);
        updated.Value.Days.Should().HaveCount(2);
        var exercise = updated.Value.Days.Single(dayItem => !dayItem.RestDay).Exercises.Single();
        exercise.Sets.Should().Be(4);
        exercise.TargetWeight.Should().Be(80m);
        exercise.Notes.Should().Be("Slow eccentric");
        (await db.AuditLogs.CountAsync(item => item.Action == "MemberProgramUpdated")).Should().Be(1);
    }

    [Fact]
    public async Task UnassignedTrainer_CannotAccessMemberPrograms()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, assignTrainer: false);
        var program = WorkoutProgram.Create(Guid.NewGuid(), _tenant, _memberUser, "Hidden", null, FitnessGoalType.GeneralFitness, WorkoutDifficulty.Beginner, 4, 3);
        db.WorkoutPrograms.Add(program);
        await db.SaveChangesAsync();

        var listed = await harness.Handlers.Handle(new ListMemberProgramsQuery(harness.MemberId), CancellationToken.None);
        var got = await harness.Handlers.Handle(new GetMemberProgramQuery(harness.MemberId, program.Id), CancellationToken.None);
        var updated = await harness.Handlers.Handle(
            new UpdateMemberProgramCommand(
                harness.MemberId,
                program.Id,
                "Nope",
                null,
                FitnessGoalType.GeneralFitness,
                WorkoutDifficulty.Beginner,
                4,
                3,
                [new ProgramDayInput(1, "Rest", null, true, [])]),
            CancellationToken.None);

        listed.IsSuccess.Should().BeFalse();
        listed.Error.Code.Should().Be(GymErrors.NotFound.Code);
        got.IsSuccess.Should().BeFalse();
        got.Error.Code.Should().Be(GymErrors.NotFound.Code);
        updated.IsSuccess.Should().BeFalse();
        updated.Error.Code.Should().Be(GymErrors.NotFound.Code);
    }

    [Fact]
    public async Task AssignedTrainer_CanCreateAndDeleteMemberProgram()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, assignTrainer: true);
        var exerciseId = Guid.NewGuid();
        db.Exercises.Add(Exercise.CreateCustom(exerciseId, _tenant, "Bench", null, null, MuscleGroup.Chest, "Barbell", ExerciseType.Strength, WorkoutDifficulty.Intermediate));
        await db.SaveChangesAsync();

        var created = await harness.Handlers.Handle(
            new CreateMemberProgramCommand(
                harness.MemberId,
                "Coach Plan",
                "Built by trainer",
                FitnessGoalType.MuscleGain,
                WorkoutDifficulty.Intermediate,
                4,
                3,
                [
                    new ProgramDayInput(1, "Push", null, false, [new ProgramExerciseInput(exerciseId, 3, 10, null, null, 50m, 90, "Pause")]),
                    new ProgramDayInput(2, "Rest", null, true, []),
                ]),
            CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        created.Value.Name.Should().Be("Coach Plan");
        created.Value.Days.Should().HaveCount(2);
        created.Value.Days.Single(day => !day.RestDay).Exercises.Single().TargetWeight.Should().Be(50m);
        (await db.AuditLogs.CountAsync(item => item.Action == "MemberProgramCreated")).Should().Be(1);

        var deleted = await harness.Handlers.Handle(new DeleteMemberProgramCommand(harness.MemberId, created.Value.Id), CancellationToken.None);
        var listed = await harness.Handlers.Handle(new ListMemberProgramsQuery(harness.MemberId), CancellationToken.None);
        var got = await harness.Handlers.Handle(new GetMemberProgramQuery(harness.MemberId, created.Value.Id), CancellationToken.None);

        deleted.IsSuccess.Should().BeTrue();
        deleted.Value.Should().Be(created.Value.Id);
        listed.Value.Items.Should().BeEmpty();
        got.IsSuccess.Should().BeFalse();
        (await db.AuditLogs.CountAsync(item => item.Action == "MemberProgramDeleted")).Should().Be(1);
    }

    [Fact]
    public async Task UnassignedTrainer_CannotCreateOrDeleteMemberProgram()
    {
        await using var db = CreateDb();
        var harness = await SeedAsync(db, assignTrainer: false);
        var program = WorkoutProgram.Create(Guid.NewGuid(), _tenant, _memberUser, "Keep", null, FitnessGoalType.GeneralFitness, WorkoutDifficulty.Beginner, 4, 3);
        db.WorkoutPrograms.Add(program);
        await db.SaveChangesAsync();

        var created = await harness.Handlers.Handle(
            new CreateMemberProgramCommand(
                harness.MemberId,
                "Nope",
                null,
                FitnessGoalType.GeneralFitness,
                WorkoutDifficulty.Beginner,
                4,
                3,
                [new ProgramDayInput(1, "Rest", null, true, [])]),
            CancellationToken.None);
        var deleted = await harness.Handlers.Handle(new DeleteMemberProgramCommand(harness.MemberId, program.Id), CancellationToken.None);

        created.IsSuccess.Should().BeFalse();
        created.Error.Code.Should().Be(GymErrors.NotFound.Code);
        deleted.IsSuccess.Should().BeFalse();
        deleted.Error.Code.Should().Be(GymErrors.NotFound.Code);
        (await db.WorkoutPrograms.CountAsync(item => item.Id == program.Id)).Should().Be(1);
    }

    private async Task<Harness> SeedAsync(ApplicationDbContext db, bool assignTrainer)
    {
        db.Tenants.Add(Tenant.Create(_tenant, "A", TenantType.Gym));
        db.Users.Add(UserAccount.Create(_actor, _tenant, "coach@example.com", null, "hash", "salt", "Rahul", "Kumar"));
        db.Users.Add(UserAccount.Create(_memberUser, _tenant, "member@example.com", null, "hash", "salt", "Priya", "Sharma"));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, _actor, DateTime.UtcNow));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenant, _memberUser, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var trainer = await new TrainerWorkspace(db).CreateFromUserAsync(_tenant, _actor, "T-1", "Strength", null, _today, true, CancellationToken.None);
        var member = MemberProfile.Create(Guid.NewGuid(), _tenant, _memberUser, "M-1", _today, "Priya", "Sharma", null);
        if (assignTrainer)
        {
            member.Update(null, null, null, null, null, null, null, null, ProfileStatus.Active, assignedTrainerId: trainer!.Id);
        }

        db.MemberProfiles.Add(member);
        await db.SaveChangesAsync();

        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenant);
        actor.UserId.Returns(_actor);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(_today.Year, _today.Month, _today.Day, 12, 0, 0, DateTimeKind.Utc));
        var client = Substitute.For<IClientContext>();
        client.IpAddress.Returns("127.0.0.1");
        client.UserAgent.Returns("test");
        var handlers = new TrainerMemberProgramHandlers(
            actor,
            new TrainerMobileStore(db),
            new FitnessRepository(db),
            new WorkoutRepository(db),
            new AuditLogRepository(db),
            new Unit(db),
            clock,
            client);
        return new Harness(handlers, member.Id);
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

    private sealed record Harness(TrainerMemberProgramHandlers Handlers, Guid MemberId);

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
