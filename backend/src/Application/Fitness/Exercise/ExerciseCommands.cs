using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Fitness.Exercises;

public sealed record ListExercisesQuery(string? Search, int Page, int PageSize) : IRequest<Result<PageResult<ExerciseResponse>>>;

public sealed class ListExercisesQueryHandler : IRequestHandler<ListExercisesQuery, Result<PageResult<ExerciseResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;

    public ListExercisesQueryHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
    }

    public async Task<Result<PageResult<ExerciseResponse>>> Handle(ListExercisesQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.ExerciseRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<ExerciseResponse>>(scope.Error);
        }

        var page = await _fitness.ListExercisesAsync(scope.Value.TenantId, request.Search, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(new PageResult<ExerciseResponse>(page.Items.Select(FitnessMaps.ToResponse).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
}

public sealed record GetExerciseQuery(Guid Id) : IRequest<Result<ExerciseResponse>>;

public sealed class GetExerciseQueryHandler : IRequestHandler<GetExerciseQuery, Result<ExerciseResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;

    public GetExerciseQueryHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
    }

    public async Task<Result<ExerciseResponse>> Handle(GetExerciseQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.ExerciseRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ExerciseResponse>(scope.Error);
        }

        var exercise = await _fitness.GetExerciseAsync(scope.Value.TenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return exercise is null ? Result.Failure<ExerciseResponse>(FitnessErrors.NotFound) : Result.Success(FitnessMaps.ToResponse(exercise));
    }
}

public sealed record CreateExerciseCommand(string Name, string? Description, string? Instructions, MuscleGroup MuscleGroup, string? Equipment, ExerciseType ExerciseType, WorkoutDifficulty Difficulty) : IRequest<Result<ExerciseResponse>>;

public sealed class CreateExerciseCommandValidator : AbstractValidator<CreateExerciseCommand>
{
    public CreateExerciseCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.Instructions).MaximumLength(2000);
        RuleFor(command => command.Equipment).MaximumLength(100);
    }
}

public sealed class CreateExerciseCommandHandler : IRequestHandler<CreateExerciseCommand, Result<ExerciseResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;
    private readonly IUnitOfWork _unitOfWork;

    public CreateExerciseCommandHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ExerciseResponse>> Handle(CreateExerciseCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.ExerciseManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ExerciseResponse>(scope.Error);
        }

        var exercise = Domain.Fitness.Exercise.CreateCustom(Guid.NewGuid(), scope.Value.TenantId, request.Name, request.Description, request.Instructions, request.MuscleGroup, request.Equipment, request.ExerciseType, request.Difficulty);
        await _fitness.AddExerciseAsync(exercise, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(FitnessMaps.ToResponse(exercise));
    }
}
