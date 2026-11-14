using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Fitness.Assessment;

public sealed record GetFitnessAssessmentQuery : IRequest<Result<FitnessAssessmentResponse>>;

public sealed record GetFitnessAssessmentCompletionQuery : IRequest<Result<FitnessAssessmentCompletionResponse>>;

public sealed record DeleteFitnessAssessmentCommand : IRequest<Result<Guid>>;

public sealed record SaveFitnessAssessmentCommand(
    DateOnly? DateOfBirth,
    string? Gender,
    decimal? HeightCm,
    decimal? CurrentWeightKg,
    decimal? TargetWeightKg,
    decimal? WaistCircumferenceCm,
    decimal? BodyFatPercentage,
    string? Country,
    UnitSystem MeasurementUnit,
    string? PrimaryGoal,
    string? SecondaryGoal,
    string? GoalTimeline,
    int? CustomGoalTimelineDays,
    FitnessLevel? FitnessLevel,
    string? Priority,
    string? GoalDescription,
    AssessmentHealthSection? Health,
    AssessmentWorkoutSection? Workout,
    AssessmentDietSection? Diet,
    AssessmentLifestyleSection? Lifestyle,
    AssessmentMeasurementsSection? Measurements,
    int CurrentStep,
    bool Submit) : IRequest<Result<FitnessAssessmentResponse>>;

public sealed class SaveFitnessAssessmentCommandValidator : AbstractValidator<SaveFitnessAssessmentCommand>
{
    private static readonly HashSet<string> Goals =
    [
        "WeightLoss", "MuscleGain", "Recomposition", "WeightMaintenance", "Strength", "Endurance", "GeneralFitness", "AthleticPerformance", "FatLoss"
    ];

    public SaveFitnessAssessmentCommandValidator()
    {
        RuleFor(command => command.DateOfBirth)
            .Must(date => date is null || date < DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.");
        RuleFor(command => command.DateOfBirth)
            .Must(date => date is null || date > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-120))
            .WithMessage("Date of birth is invalid.");
        RuleFor(command => command.HeightCm).InclusiveBetween(50, 300).When(command => command.HeightCm.HasValue);
        RuleFor(command => command.CurrentWeightKg).InclusiveBetween(20, 500).When(command => command.CurrentWeightKg.HasValue);
        RuleFor(command => command.TargetWeightKg).InclusiveBetween(20, 500).When(command => command.TargetWeightKg.HasValue);
        RuleFor(command => command.BodyFatPercentage).InclusiveBetween(1, 70).When(command => command.BodyFatPercentage.HasValue);
        RuleFor(command => command.Gender).MaximumLength(32);
        RuleFor(command => command.Country).MaximumLength(100);
        RuleFor(command => command.PrimaryGoal).Must(goal => goal is null || Goals.Contains(goal)).WithMessage("Primary goal is invalid.");
        RuleFor(command => command.SecondaryGoal).Must(goal => goal is null || Goals.Contains(goal)).WithMessage("Secondary goal is invalid.");
        RuleFor(command => command.GoalDescription).MaximumLength(1000);
        RuleFor(command => command.CurrentStep).InclusiveBetween(1, 8);
        RuleFor(command => command.CustomGoalTimelineDays).InclusiveBetween(1, 730).When(command => command.CustomGoalTimelineDays.HasValue);
        RuleFor(command => command.Diet).Custom((diet, context) =>
        {
            if (diet is null)
            {
                return;
            }

            if (string.Equals(diet.DietType, "Vegan", StringComparison.OrdinalIgnoreCase) &&
                (diet.EggsAllowed == true || string.Equals(diet.DairyConsumption, "Yes", StringComparison.OrdinalIgnoreCase)))
            {
                context.AddFailure(nameof(SaveFitnessAssessmentCommand.Diet), "Vegan assessments cannot include eggs or full dairy.");
            }

            if (diet.MealsPerDay is int meals && (meals < 2 || meals > 8))
            {
                context.AddFailure(nameof(SaveFitnessAssessmentCommand.Diet), "Meals per day must be between 2 and 8.");
            }
        });
        RuleFor(command => command.Workout!.DaysPerWeek).InclusiveBetween(1, 7).When(command => command.Workout?.DaysPerWeek is not null);
        When(command => command.Submit, () =>
        {
            RuleFor(command => command.DateOfBirth).NotNull();
            RuleFor(command => command.HeightCm).NotNull();
            RuleFor(command => command.CurrentWeightKg).NotNull();
            RuleFor(command => command.PrimaryGoal).NotEmpty();
            RuleFor(command => command.FitnessLevel).NotNull();
            RuleFor(command => command.Diet!.DietType).NotEmpty().When(command => command.Diet is not null);
        });
    }
}

public sealed class FitnessAssessmentHandlers :
    IRequestHandler<GetFitnessAssessmentQuery, Result<FitnessAssessmentResponse>>,
    IRequestHandler<GetFitnessAssessmentCompletionQuery, Result<FitnessAssessmentCompletionResponse>>,
    IRequestHandler<SaveFitnessAssessmentCommand, Result<FitnessAssessmentResponse>>,
    IRequestHandler<DeleteFitnessAssessmentCommand, Result<Guid>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public FitnessAssessmentHandlers(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<FitnessAssessmentResponse>> Handle(GetFitnessAssessmentQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<FitnessAssessmentResponse>(scope.Error);
        }

        var assessment = await _fitness.GetAssessmentAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        return assessment is null
            ? Result.Failure<FitnessAssessmentResponse>(FitnessErrors.NotFound)
            : Result.Success(AssessmentMaps.ToResponse(assessment, _clock));
    }

    public async Task<Result<FitnessAssessmentCompletionResponse>> Handle(GetFitnessAssessmentCompletionQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<FitnessAssessmentCompletionResponse>(scope.Error);
        }

        var assessment = await _fitness.GetAssessmentAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        return assessment is null
            ? Result.Success(new FitnessAssessmentCompletionResponse(false, false, 1, 0))
            : Result.Success(new FitnessAssessmentCompletionResponse(true, assessment.IsCompleted, assessment.CurrentStep, assessment.CompletionPercentage));
    }

    public async Task<Result<FitnessAssessmentResponse>> Handle(SaveFitnessAssessmentCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<FitnessAssessmentResponse>(scope.Error);
        }

        var assessment = await _fitness.GetAssessmentAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        if (assessment is null)
        {
            assessment = FitnessAssessment.Create(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId);
            await _fitness.AddAssessmentAsync(assessment, cancellationToken).ConfigureAwait(false);
        }

        var completion = AssessmentMaps.CompletionPercentage(
            request.DateOfBirth,
            request.HeightCm,
            request.CurrentWeightKg,
            request.PrimaryGoal,
            request.FitnessLevel,
            request.Health,
            request.Workout,
            request.Diet,
            request.Lifestyle,
            request.Measurements);

        assessment.ApplyDraft(
            request.DateOfBirth,
            request.Gender,
            request.HeightCm,
            request.CurrentWeightKg,
            request.TargetWeightKg,
            request.WaistCircumferenceCm,
            request.BodyFatPercentage,
            request.Country,
            request.MeasurementUnit,
            request.PrimaryGoal,
            request.SecondaryGoal,
            request.GoalTimeline,
            request.CustomGoalTimelineDays,
            request.FitnessLevel,
            request.Priority,
            request.GoalDescription,
            AssessmentJson.Serialize(request.Health),
            AssessmentJson.Serialize(request.Workout),
            AssessmentJson.Serialize(request.Diet),
            AssessmentJson.Serialize(request.Lifestyle),
            AssessmentJson.Serialize(request.Measurements),
            request.CurrentStep,
            request.Submit ? 100 : completion,
            request.Submit);

        if (request.Submit)
        {
            await MirrorProfile(scope.Value.TenantId, scope.Value.UserId, assessment, request, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(AssessmentMaps.ToResponse(assessment, _clock));
    }

    public async Task<Result<Guid>> Handle(DeleteFitnessAssessmentCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<Guid>(scope.Error);
        }

        var assessment = await _fitness.GetAssessmentAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        if (assessment is null)
        {
            return Result.Failure<Guid>(FitnessErrors.NotFound);
        }

        assessment.SoftDelete(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(assessment.Id);
    }

    private async Task MirrorProfile(Guid tenantId, Guid userId, FitnessAssessment assessment, SaveFitnessAssessmentCommand request, CancellationToken cancellationToken)
    {
        var profile = await _fitness.GetProfileAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            profile = FitnessProfile.Create(Guid.NewGuid(), userId, tenantId);
            await _fitness.AddProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        }

        var days = request.Workout?.DaysPerWeek ?? 3;
        var duration = request.Workout?.DurationMinutes ?? 45;
        var location = request.Workout?.Location;
        profile.Update(
            assessment.MeasurementUnit,
            assessment.HeightCm,
            assessment.CurrentWeightKg,
            assessment.FitnessLevel ?? FitnessLevel.Beginner,
            AssessmentMaps.MapGoal(assessment.PrimaryGoal),
            AssessmentMaps.MapActivity(request.Lifestyle?.ActivityLevel),
            request.Workout?.Experience,
            days,
            duration,
            location);
    }
}
