using FluentAssertions;
using LifeFitAI.Application.Ai;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Fitness.Ai;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Fitness;

public sealed class WorkoutGenerationTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _exerciseId = Guid.Parse("33333333-3333-4333-8333-333333333001");
    private readonly DateTime _now = new(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Brief_UsesTheCompletedAssessmentAndCalculatesEnergy()
    {
        var brief = AssessmentWorkoutBrief.Create(Assessment("Strength", FitnessLevel.Intermediate, """{"location":"Home","daysPerWeek":3,"durationMinutes":40,"equipment":["Dumbbells"]}""", """{"activityLevel":3}"""), new DateOnly(2026, 11, 14));

        brief.IsSuccess.Should().BeTrue();
        brief.Value.Age.Should().Be(30);
        brief.Value.Bmi.Should().Be(24.7m);
        brief.Value.Bmr.Should().Be(1780m);
        brief.Value.Tdee.Should().Be(2759m);
        brief.Value.WeeklyMinutes.Should().Be(120);
        brief.Value.PrimaryGoal.Should().Be("Strength");
        var json = AssessmentWorkoutBrief.ToJson(brief.Value, ["Goblet Squat"]);
        json.Should().Contain("1780");
        json.Should().Contain("Goblet Squat");
        json.Should().NotContain("medication");
    }

    [Fact]
    public void Brief_RequiresACompletedAssessmentAndStopsForClearance()
    {
        AssessmentWorkoutBrief.Create(null, new DateOnly(2026, 11, 14)).Error.Code.Should().Be("ai.assessment_required");
        var draft = FitnessAssessment.Create(Guid.NewGuid(), _userId, _tenantId);
        AssessmentWorkoutBrief.Create(draft, new DateOnly(2026, 11, 14)).Error.Code.Should().Be("ai.assessment_required");
        AssessmentWorkoutBrief.Create(Assessment("Strength", FitnessLevel.Beginner, "{}", null, """{"doctorRestrictions":"Needs medical clearance before exercise"}"""), new DateOnly(2026, 11, 14)).Error.Code.Should().Be("ai.medical_clearance");
    }

    [Fact]
    public void Equipment_KeepsHomeGearAndDropsGymMachines()
    {
        var catalog = new List<GenerationExercise>
        {
            new(Guid.NewGuid(), "Barbell Bench Press", "Strength", "Chest", "Barbell", "Intermediate"),
            new(Guid.NewGuid(), "Push-up", "Bodyweight", "Chest", "Bodyweight", "Beginner"),
            new(_exerciseId, "Dumbbell Row", "Strength", "Back", "Dumbbell", "Beginner"),
        };

        var home = AssessmentWorkoutBrief.MatchEquipment(catalog, ["Dumbbells", "BodyweightOnly"], "Home");
        home.Select(item => item.Name).Should().BeEquivalentTo("Push-up", "Dumbbell Row");
        AssessmentWorkoutBrief.MatchEquipment(catalog, ["BodyweightOnly"], "Home").Select(item => item.Name).Should().Equal("Push-up");
        AssessmentWorkoutBrief.MatchEquipment(catalog, ["FullGym"], "Gym").Should().HaveCount(3);
    }

    [Fact]
    public void MonthParser_RequiresThirtyProgressiveDaysAndRejectsBadJson()
    {
        var names = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["Push-up"] = _exerciseId };
        var allowed = new HashSet<Guid> { _exerciseId };
        WorkoutPlanParser.ParseMonth(MonthJson(false), allowed, names).IsSuccess.Should().BeTrue();
        WorkoutPlanParser.ParseMonth("{\"name\":\"Week\",\"days\":[{\"dayNumber\":1,\"name\":\"Push\",\"restDay\":false,\"exercises\":[{\"name\":\"Push-up\",\"sets\":3,\"reps\":8}]}]}", allowed, names).IsFailure.Should().BeTrue();
        WorkoutPlanParser.ParseMonth("not json", allowed, names).Error.Code.Should().Be("ai.invalid_output");
        WorkoutMonthRules.Validate(WorkoutPlanParser.ParseMonth(MonthJson(false), allowed, names).Value, 5, new Dictionary<Guid, string> { [_exerciseId] = "Push-up" }, null).IsSuccess.Should().BeTrue();
        WorkoutMonthRules.Validate(WorkoutPlanParser.ParseMonth(MonthJson(true), allowed, names).Value, 5, new Dictionary<Guid, string> { [_exerciseId] = "Push-up" }, null).Error.Description.Should().Contain("progression");
        WorkoutMonthRules.Validate(WorkoutPlanParser.ParseMonth(MonthJson(false), allowed, names).Value, 2, new Dictionary<Guid, string> { [_exerciseId] = "Push-up" }, null).Error.Description.Should().Contain("frequency");
        WorkoutMonthRules.Validate(WorkoutPlanParser.ParseMonth(MonthJson(false), allowed, names).Value, 5, new Dictionary<Guid, string> { [_exerciseId] = "Behind the Neck Press" }, "behind the neck").Error.Description.Should().Contain("avoid");
    }

    [Fact]
    public async Task Generation_RejectsAMissingAssessmentBeforeCallingTheProvider()
    {
        var provider = Substitute.For<IAiProvider>();
        var fitness = Substitute.For<IFitnessRepository>();
        var handler = Handler(provider, fitness, Substitute.For<IWorkoutRepository>(), Substitute.For<IUnitOfWork>());

        var result = await handler.Handle(new GenerateWorkoutCommand(null), CancellationToken.None);

        result.Error.Code.Should().Be("ai.assessment_required");
        await provider.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generation_StoresANewPlanAndKeepsEarlierPlans()
    {
        var provider = Substitute.For<IAiProvider>();
        provider.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>()).Returns(new AiCompletionResult(true, MonthJson(false), 20, 800, string.Empty, "Groq", "test-model"));
        var workouts = Substitute.For<IWorkoutRepository>();
        var saved = new List<WorkoutProgram>();
        var exercises = new List<WorkoutProgramExercise>();
        workouts.AddProgramAsync(Arg.Do<WorkoutProgram>(saved.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        workouts.AddDayAsync(Arg.Any<WorkoutProgramDay>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        workouts.AddProgramExerciseAsync(Arg.Do<WorkoutProgramExercise>(exercises.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        workouts.GetProgramAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ProgramResponse(Guid.NewGuid(), "30-Day Plan", null, FitnessGoalType.Strength, WorkoutDifficulty.Intermediate, 5, 5, ProgramStatus.Active, true, []));
        var unit = Substitute.For<IUnitOfWork>();
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        var handler = Handler(provider, FitnessWith(Assessment("Strength", FitnessLevel.Intermediate, """{"location":"Home","daysPerWeek":5,"durationMinutes":45,"equipment":["BodyweightOnly"],"exercisesToAvoid":"burpee"}""", """{"activityLevel":2}""")), workouts, unit);

        var first = await handler.Handle(new GenerateWorkoutCommand("keep the ankle easy"), CancellationToken.None);
        var second = await handler.Handle(new GenerateWorkoutCommand("keep the ankle easy"), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        saved.Should().HaveCount(2);
        saved.Select(item => item.Id).Distinct().Should().HaveCount(2);
        saved[0].SourceAssessmentId.Should().NotBeNull();
        saved[0].PromptVersion.Should().Be(FitnessPrompts.WorkoutMonthVersion);
        saved[0].GenerationModel.Should().Be("test-model");
        saved[0].PlanStartDate.Should().Be(new DateOnly(2026, 11, 14));
        saved[0].GoalType.Should().Be(FitnessGoalType.Strength);
        saved[0].Difficulty.Should().Be(WorkoutDifficulty.Intermediate);
        exercises.Should().NotBeEmpty();
        exercises.Should().OnlyContain(item => item.TargetWeight == null && item.Notes != null && item.Notes.Contains("RPE"));
        await workouts.Received(60).AddDayAsync(Arg.Any<WorkoutProgramDay>(), Arg.Any<CancellationToken>());
        await workouts.DidNotReceive().SoftDeleteProgramAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    private AiCommandHandler Handler(IAiProvider provider, IFitnessRepository fitness, IWorkoutRepository workouts, IUnitOfWork unit)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_userId);
        actor.TenantId.Returns(_tenantId);
        var limits = Substitute.For<IAiLimits>();
        limits.Provider.Returns("Test");
        limits.Model.Returns("test-model");
        limits.MaxPromptCharacters.Returns(8000);
        limits.CostPerThousandTokens.Returns(0m);
        var quota = Substitute.For<IAiQuotaService>();
        quota.AllowAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var tenants = Substitute.For<ITenantRepository>();
        tenants.GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(Tenant.Create(_tenantId, "Ada", TenantType.Platform));
        var ai = Substitute.For<IAiRepository>();
        ai.AddUsageAsync(Arg.Any<AiUsageRecord>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_now);
        return new AiCommandHandler(guard, actor, provider, limits, new AiSafetyService(), quota, Substitute.For<IAiContextBuilder>(), ai, fitness, workouts, tenants, Substitute.For<IAuditLogRepository>(), unit, clock, Substitute.For<IClientContext>());
    }

    private IFitnessRepository FitnessWith(FitnessAssessment assessment)
    {
        var fitness = Substitute.For<IFitnessRepository>();
        fitness.GetAssessmentAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns(assessment);
        fitness.ListGenerationExercisesAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(new List<GenerationExercise> { new(_exerciseId, "Push-up", "Bodyweight", "Chest", "Bodyweight", "Beginner") });
        return fitness;
    }

    private FitnessAssessment Assessment(string goal, FitnessLevel level, string workout, string? lifestyle, string? health = null)
    {
        var assessment = FitnessAssessment.Create(Guid.NewGuid(), _userId, _tenantId);
        assessment.ApplyDraft(new DateOnly(1996, 1, 1), "Male", 180, 80, 75, null, 18, "Nepal", UnitSystem.Metric, goal, "Mobility", "OneMonth", null, level, "Strength", "Stay strong", health ?? """{"injuryDetails":"old ankle sprain"}""", workout, """{"dietType":"Vegetarian","supplements":"creatine"}""", lifestyle, null, 8, 100, true);
        return assessment;
    }

    private string MonthJson(bool identical)
    {
        var days = Enumerable.Range(1, 30).Select(day =>
        {
            if (day % 7 is 0 or 6)
            {
                return $$"""{"dayNumber":{{day}},"name":"Rest","restDay":true,"description":"Walk and sleep","exercises":[]}""";
            }

            var week = identical ? 1 : ((day - 1) / 7) + 1;
            return $$"""{"dayNumber":{{day}},"name":"Full body {{week}}","restDay":false,"exercises":[{"name":"Push-up","sets":{{week + 1}},"reps":"{{8 + week}}-{{10 + week}}","restSeconds":90,"rpe":7}]}""";
        });
        return "{\"name\":\"30-Day Plan\",\"days\":[" + string.Join(',', days) + "]}";
    }
}
