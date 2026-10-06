using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Fitness.Profile;

public sealed record GetFitnessProfileQuery : IRequest<Result<FitnessProfileResponse>>;

public sealed class GetFitnessProfileQueryHandler : IRequestHandler<GetFitnessProfileQuery, Result<FitnessProfileResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;

    public GetFitnessProfileQueryHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
    }

    public async Task<Result<FitnessProfileResponse>> Handle(GetFitnessProfileQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<FitnessProfileResponse>(scope.Error);
        }

        var profile = await _fitness.GetProfileAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        return profile is null
            ? Result.Failure<FitnessProfileResponse>(FitnessErrors.NotFound)
            : Result.Success(FitnessMaps.ToResponse(profile));
    }
}

public sealed record SaveFitnessProfileCommand(UnitSystem UnitSystem, decimal? Height, decimal? Weight, FitnessLevel FitnessLevel, FitnessGoalType PrimaryGoal, ActivityLevel ActivityLevel, string? TrainingExperience, int PreferredWorkoutDays, int PreferredWorkoutDuration, string? PreferredTrainingLocation) : IRequest<Result<FitnessProfileResponse>>;

public sealed class SaveFitnessProfileCommandValidator : AbstractValidator<SaveFitnessProfileCommand>
{
    public SaveFitnessProfileCommandValidator()
    {
        RuleFor(command => command.Height).InclusiveBetween(1, 300).When(command => command.Height.HasValue);
        RuleFor(command => command.Weight).InclusiveBetween(1, 500).When(command => command.Weight.HasValue);
        RuleFor(command => command.PreferredWorkoutDays).InclusiveBetween(0, 7);
        RuleFor(command => command.PreferredWorkoutDuration).InclusiveBetween(0, 300);
        RuleFor(command => command.TrainingExperience).MaximumLength(500);
        RuleFor(command => command.PreferredTrainingLocation).MaximumLength(100);
    }
}

public sealed class SaveFitnessProfileCommandHandler : IRequestHandler<SaveFitnessProfileCommand, Result<FitnessProfileResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;
    private readonly IUnitOfWork _unitOfWork;

    public SaveFitnessProfileCommandHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FitnessProfileResponse>> Handle(SaveFitnessProfileCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<FitnessProfileResponse>(scope.Error);
        }

        var profile = await _fitness.GetProfileAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            profile = FitnessProfile.Create(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId);
            await _fitness.AddProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        }

        profile.Update(request.UnitSystem, request.Height, request.Weight, request.FitnessLevel, request.PrimaryGoal, request.ActivityLevel, request.TrainingExperience, request.PreferredWorkoutDays, request.PreferredWorkoutDuration, request.PreferredTrainingLocation);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(FitnessMaps.ToResponse(profile));
    }
}

public sealed record ListGoalsQuery : IRequest<Result<IReadOnlyList<GoalResponse>>>;

public sealed class ListGoalsQueryHandler : IRequestHandler<ListGoalsQuery, Result<IReadOnlyList<GoalResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;

    public ListGoalsQueryHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
    }

    public async Task<Result<IReadOnlyList<GoalResponse>>> Handle(ListGoalsQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GoalResponse>>(scope.Error);
        }

        var goals = await _fitness.ListGoalsAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<GoalResponse>>(goals.Select(FitnessMaps.ToResponse).ToArray());
    }
}

public sealed record CreateGoalCommand(FitnessGoalType GoalType, decimal? TargetValue, decimal? CurrentValue, DateOnly StartDate, DateOnly? TargetDate) : IRequest<Result<GoalResponse>>;

public sealed class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, Result<GoalResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGoalCommandHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GoalResponse>> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<GoalResponse>(scope.Error);
        }

        var goal = FitnessGoal.Create(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, request.GoalType, request.TargetValue, request.CurrentValue, request.StartDate, request.TargetDate);
        await _fitness.AddGoalAsync(goal, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(FitnessMaps.ToResponse(goal));
    }
}

public sealed record UpdateGoalCommand(Guid Id, decimal? TargetValue, decimal? CurrentValue, DateOnly? TargetDate) : IRequest<Result<GoalResponse>>;

public sealed record CompleteGoalCommand(Guid Id) : IRequest<Result<GoalResponse>>;

public sealed record CancelGoalCommand(Guid Id) : IRequest<Result<GoalResponse>>;

public sealed class UpdateGoalCommandHandler :
    IRequestHandler<UpdateGoalCommand, Result<GoalResponse>>,
    IRequestHandler<CompleteGoalCommand, Result<GoalResponse>>,
    IRequestHandler<CancelGoalCommand, Result<GoalResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGoalCommandHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
        _unitOfWork = unitOfWork;
    }

    public Task<Result<GoalResponse>> Handle(UpdateGoalCommand request, CancellationToken cancellationToken) =>
        Change(request.Id, goal => goal.Update(request.TargetValue, request.CurrentValue, request.TargetDate), cancellationToken);

    public Task<Result<GoalResponse>> Handle(CompleteGoalCommand request, CancellationToken cancellationToken) =>
        Change(request.Id, goal => goal.Complete(), cancellationToken);

    public Task<Result<GoalResponse>> Handle(CancelGoalCommand request, CancellationToken cancellationToken) =>
        Change(request.Id, goal => goal.Cancel(), cancellationToken);

    private async Task<Result<GoalResponse>> Change(Guid id, Func<FitnessGoal, bool> change, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.FitnessManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<GoalResponse>(scope.Error);
        }

        var goal = await _fitness.GetGoalAsync(scope.Value.TenantId, scope.Value.UserId, id, cancellationToken).ConfigureAwait(false);
        if (goal is null)
        {
            return Result.Failure<GoalResponse>(FitnessErrors.NotFound);
        }

        if (!change(goal))
        {
            return Result.Failure<GoalResponse>(FitnessErrors.InvalidState);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(FitnessMaps.ToResponse(goal));
    }
}

