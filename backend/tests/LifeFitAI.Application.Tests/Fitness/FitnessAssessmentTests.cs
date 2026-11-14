using FluentAssertions;
using FluentValidation.TestHelper;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Fitness.Assessment;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Fitness;

public sealed class FitnessAssessmentTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Save_CreatesDraftAndPersistsForCurrentUser()
    {
        var fitness = Substitute.For<IFitnessRepository>();
        fitness.GetAssessmentAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((FitnessAssessment?)null);
        FitnessAssessment? stored = null;
        fitness.AddAssessmentAsync(Arg.Do<FitnessAssessment>(item => stored = item), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var unit = Substitute.For<IUnitOfWork>();
        var handler = new FitnessAssessmentHandlers(Allow(), Actor(), fitness, unit, Clock());

        var result = await handler.Handle(DraftCommand(submit: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompleted.Should().BeFalse();
        result.Value.CompletionPercentage.Should().BeGreaterThan(0);
        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(_userId);
        stored.TenantId.Should().Be(_tenantId);
        await unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_MirrorsCoreFieldsIntoFitnessProfile()
    {
        var fitness = Substitute.For<IFitnessRepository>();
        fitness.GetAssessmentAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((FitnessAssessment?)null);
        fitness.GetProfileAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((FitnessProfile?)null);
        FitnessProfile? profile = null;
        fitness.AddProfileAsync(Arg.Do<FitnessProfile>(item => profile = item), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var handler = new FitnessAssessmentHandlers(Allow(), Actor(), fitness, Substitute.For<IUnitOfWork>(), Clock());

        var result = await handler.Handle(DraftCommand(submit: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompleted.Should().BeTrue();
        result.Value.CompletionPercentage.Should().Be(100);
        profile.Should().NotBeNull();
        profile!.Height.Should().Be(175);
        profile.Weight.Should().Be(72);
        profile.PrimaryGoal.Should().Be(FitnessGoalType.FatLoss);
        profile.FitnessLevel.Should().Be(FitnessLevel.Intermediate);
    }

    [Fact]
    public async Task Get_UsesCurrentTenantAndUser()
    {
        var fitness = Substitute.For<IFitnessRepository>();
        fitness.GetAssessmentAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((FitnessAssessment?)null);
        var handler = new FitnessAssessmentHandlers(Allow(), Actor(), fitness, Substitute.For<IUnitOfWork>(), Clock());

        var result = await handler.Handle(new GetFitnessAssessmentQuery(), CancellationToken.None);

        result.Error.Code.Should().Be("fitness.not_found");
        await fitness.Received(1).GetAssessmentAsync(_tenantId, _userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Completion_ReturnsEmptyWhenMissing()
    {
        var fitness = Substitute.For<IFitnessRepository>();
        fitness.GetAssessmentAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((FitnessAssessment?)null);
        var handler = new FitnessAssessmentHandlers(Allow(), Actor(), fitness, Substitute.For<IUnitOfWork>(), Clock());

        var result = await handler.Handle(new GetFitnessAssessmentCompletionQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Exists.Should().BeFalse();
        result.Value.CompletionPercentage.Should().Be(0);
    }

    [Fact]
    public async Task Delete_SoftDeletesOwnedAssessment()
    {
        var assessment = FitnessAssessment.Create(Guid.NewGuid(), _userId, _tenantId);
        var fitness = Substitute.For<IFitnessRepository>();
        fitness.GetAssessmentAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns(assessment);
        var handler = new FitnessAssessmentHandlers(Allow(), Actor(), fitness, Substitute.For<IUnitOfWork>(), Clock());

        var result = await handler.Handle(new DeleteFitnessAssessmentCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        assessment.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Validator_RejectsFutureDateOfBirth()
    {
        var validator = new SaveFitnessAssessmentCommandValidator();
        var result = validator.TestValidate(DraftCommand(submit: false) with { DateOfBirth = new DateOnly(2099, 1, 1) });
        result.ShouldHaveValidationErrorFor(command => command.DateOfBirth);
    }

    [Fact]
    public void Validator_RejectsInvalidHeightAndWeight()
    {
        var validator = new SaveFitnessAssessmentCommandValidator();
        var result = validator.TestValidate(DraftCommand(submit: false) with { HeightCm = 10, CurrentWeightKg = 5 });
        result.ShouldHaveValidationErrorFor(command => command.HeightCm);
        result.ShouldHaveValidationErrorFor(command => command.CurrentWeightKg);
    }

    [Fact]
    public void Validator_RejectsVeganDairyCombination()
    {
        var validator = new SaveFitnessAssessmentCommandValidator();
        var result = validator.TestValidate(DraftCommand(submit: false) with
        {
            Diet = new AssessmentDietSection("Vegan", true, "Yes", null, 3, null, null, null, null, null, null, null, null, null),
        });
        result.ShouldHaveValidationErrorFor(command => command.Diet);
    }

    [Fact]
    public void CompletionPercentage_CountsFilledSections()
    {
        var percent = AssessmentMaps.CompletionPercentage(
            new DateOnly(1995, 5, 1),
            175,
            72,
            "WeightLoss",
            FitnessLevel.Intermediate,
            new AssessmentHealthSection(false, null, false, null, null, null, null, null, null, null),
            new AssessmentWorkoutSection("Gym", null, 4, null, 45, null, null, null, null, null, null, null),
            new AssessmentDietSection("Vegetarian", true, "Limited", null, 3, null, null, null, null, null, null, null, null, null),
            new AssessmentLifestyleSection(null, null, ActivityLevel.Moderate, null, null, null, null, null, null),
            new AssessmentMeasurementsSection(null, null, null, null, null, null, null, null));

        percent.Should().Be(100);
    }

    private SaveFitnessAssessmentCommand DraftCommand(bool submit) =>
        new(
            new DateOnly(1995, 5, 1),
            "Female",
            175,
            72,
            65,
            70,
            null,
            "Nepal",
            UnitSystem.Metric,
            "WeightLoss",
            null,
            "ThreeMonths",
            null,
            FitnessLevel.Intermediate,
            "FatLoss",
            null,
            new AssessmentHealthSection(false, null, false, null, null, null, null, null, null, null),
            new AssessmentWorkoutSection("Gym", "Intermediate", 4, ["Monday", "Wednesday"], 45, "Morning", ["Dumbbells"], ["Strength"], null, null, null, null),
            new AssessmentDietSection("Vegetarian", true, "Limited", ["Nepali"], 3, null, null, null, null, "HomeCooking", "Medium", null, null, null),
            new AssessmentLifestyleSection("Engineer", "Sitting", ActivityLevel.Moderate, 6000, 7, "Good", "Moderate", 2.5m, null),
            new AssessmentMeasurementsSection(95, 70, 98, 30, 30, 55, null, null),
            4,
            submit);

    private IOperationGuard Allow()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenantId);
        actor.UserId.Returns(_userId);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_now);
        return clock;
    }
}
