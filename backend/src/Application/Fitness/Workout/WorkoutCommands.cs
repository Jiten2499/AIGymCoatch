using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Fitness.Workout;

public sealed record ProgramExerciseInput(Guid ExerciseId, int? Sets, int? TargetReps, int? TargetDurationSeconds, decimal? TargetDistance, decimal? TargetWeight, int? RestSeconds, string? Notes);

public sealed record ProgramDayInput(int DayNumber, string Name, string? Description, bool RestDay, IReadOnlyList<ProgramExerciseInput> Exercises);

public sealed record ListProgramsQuery(int Page, int PageSize) : IRequest<Result<PageResult<ProgramResponse>>>;

public sealed class ListProgramsQueryHandler : IRequestHandler<ListProgramsQuery, Result<PageResult<ProgramResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IWorkoutRepository _workouts;

    public ListProgramsQueryHandler(IOperationGuard guard, ICurrentActor actor, IWorkoutRepository workouts)
    {
        _guard = guard;
        _actor = actor;
        _workouts = workouts;
    }

    public async Task<Result<PageResult<ProgramResponse>>> Handle(ListProgramsQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<ProgramResponse>>(scope.Error);
        }

        var programs = await _workouts.ListProgramsAsync(scope.Value.TenantId, scope.Value.UserId, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(programs);
    }
}

public sealed record GetProgramQuery(Guid Id) : IRequest<Result<ProgramResponse>>;

public sealed class GetProgramQueryHandler : IRequestHandler<GetProgramQuery, Result<ProgramResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IWorkoutRepository _workouts;

    public GetProgramQueryHandler(IOperationGuard guard, ICurrentActor actor, IWorkoutRepository workouts)
    {
        _guard = guard;
        _actor = actor;
        _workouts = workouts;
    }

    public async Task<Result<ProgramResponse>> Handle(GetProgramQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ProgramResponse>(scope.Error);
        }

        var program = await _workouts.GetProgramAsync(scope.Value.TenantId, scope.Value.UserId, request.Id, cancellationToken).ConfigureAwait(false);
        return program is null ? Result.Failure<ProgramResponse>(FitnessErrors.NotFound) : Result.Success(program);
    }
}

public sealed record CreateProgramCommand(string Name, string? Description, FitnessGoalType GoalType, WorkoutDifficulty Difficulty, int DurationWeeks, int FrequencyPerWeek, IReadOnlyList<ProgramDayInput> Days) : IRequest<Result<ProgramResponse>>;

public sealed class CreateProgramCommandValidator : AbstractValidator<CreateProgramCommand>
{
    public CreateProgramCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.DurationWeeks).InclusiveBetween(1, 52);
        RuleFor(command => command.FrequencyPerWeek).InclusiveBetween(1, 7);
        RuleFor(command => command.Days).NotEmpty();
        RuleForEach(command => command.Days).ChildRules(day =>
        {
            day.RuleFor(item => item.DayNumber).InclusiveBetween(1, 14);
            day.RuleFor(item => item.Name).NotEmpty().MaximumLength(200);
        });
    }
}

public sealed class CreateProgramCommandHandler : IRequestHandler<CreateProgramCommand, Result<ProgramResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;
    private readonly IWorkoutRepository _workouts;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProgramCommandHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness, IWorkoutRepository workouts, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
        _workouts = workouts;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProgramResponse>> Handle(CreateProgramCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ProgramResponse>(scope.Error);
        }

        foreach (var item in request.Days.SelectMany(day => day.RestDay ? [] : day.Exercises))
        {
            if (await _fitness.GetExerciseAsync(scope.Value.TenantId, item.ExerciseId, cancellationToken).ConfigureAwait(false) is null)
            {
                return Result.Failure<ProgramResponse>(FitnessErrors.NotFound);
            }
        }

        ProgramResponse? created = null;
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var program = WorkoutProgram.Create(Guid.NewGuid(), scope.Value.TenantId, scope.Value.UserId, request.Name, request.Description, request.GoalType, request.Difficulty, request.DurationWeeks, request.FrequencyPerWeek);
            await _workouts.AddProgramAsync(program, token).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            foreach (var dayInput in request.Days.OrderBy(day => day.DayNumber))
            {
                var day = WorkoutProgramDay.Create(Guid.NewGuid(), program.Id, dayInput.DayNumber, dayInput.Name, dayInput.Description, dayInput.RestDay);
                await _workouts.AddDayAsync(day, token).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
                if (dayInput.RestDay)
                {
                    continue;
                }

                var sequence = 1;
                foreach (var item in dayInput.Exercises)
                {
                    await _workouts.AddProgramExerciseAsync(WorkoutProgramExercise.Create(Guid.NewGuid(), day.Id, item.ExerciseId, sequence++, item.Sets, item.TargetReps, item.TargetDurationSeconds, item.TargetDistance, item.TargetWeight, item.RestSeconds, item.Notes), token).ConfigureAwait(false);
                }
            }

            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            created = await _workouts.GetProgramAsync(scope.Value.TenantId, scope.Value.UserId, program.Id, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return created is null ? Result.Failure<ProgramResponse>(FitnessErrors.NotFound) : Result.Success(created);
    }
}

public sealed record StartProgramCommand(Guid ProgramId, Guid? DayId) : IRequest<Result<SessionResponse>>;

public sealed class StartProgramCommandHandler : IRequestHandler<StartProgramCommand, Result<SessionResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IWorkoutRepository _workouts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public StartProgramCommandHandler(IOperationGuard guard, ICurrentActor actor, IWorkoutRepository workouts, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _workouts = workouts;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<SessionResponse>> Handle(StartProgramCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<SessionResponse>(scope.Error);
        }

        if (!await _workouts.ProgramVisibleAsync(scope.Value.TenantId, scope.Value.UserId, request.ProgramId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<SessionResponse>(FitnessErrors.NotFound);
        }

        var program = await _workouts.GetProgramAsync(scope.Value.TenantId, scope.Value.UserId, request.ProgramId, cancellationToken).ConfigureAwait(false);
        var day = program?.Days.FirstOrDefault(item => request.DayId is null || item.Id == request.DayId);
        var session = WorkoutSession.Plan(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, request.ProgramId, day?.Id, null);
        session.Start(_clock.UtcNow);
        await _workouts.AddSessionAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (day is not null && !day.RestDay)
        {
            var sequence = 1;
            foreach (var exercise in day.Exercises)
            {
                await _workouts.AddSessionExerciseAsync(WorkoutSessionExercise.Create(Guid.NewGuid(), session.Id, exercise.ExerciseId, sequence++, exercise.Notes, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var detail = await _workouts.GetSessionAsync(scope.Value.TenantId, scope.Value.UserId, session.Id, cancellationToken).ConfigureAwait(false);
        return detail is null ? Result.Failure<SessionResponse>(FitnessErrors.NotFound) : Result.Success(detail);
    }
}
