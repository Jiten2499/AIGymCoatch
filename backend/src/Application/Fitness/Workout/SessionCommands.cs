using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Fitness.Workout;

public sealed record ListSessionsQuery(SessionStatus? Status, int Page, int PageSize) : IRequest<Result<PageResult<SessionResponse>>>;

public sealed class ListSessionsQueryHandler : IRequestHandler<ListSessionsQuery, Result<PageResult<SessionResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IWorkoutRepository _workouts;

    public ListSessionsQueryHandler(IOperationGuard guard, ICurrentActor actor, IWorkoutRepository workouts)
    {
        _guard = guard;
        _actor = actor;
        _workouts = workouts;
    }

    public async Task<Result<PageResult<SessionResponse>>> Handle(ListSessionsQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<SessionResponse>>(scope.Error);
        }

        return Result.Success(await _workouts.ListSessionsAsync(scope.Value.TenantId, scope.Value.UserId, request.Status, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false));
    }
}

public sealed record GetSessionQuery(Guid Id) : IRequest<Result<SessionResponse>>;

public sealed class GetSessionQueryHandler : IRequestHandler<GetSessionQuery, Result<SessionResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IWorkoutRepository _workouts;

    public GetSessionQueryHandler(IOperationGuard guard, ICurrentActor actor, IWorkoutRepository workouts)
    {
        _guard = guard;
        _actor = actor;
        _workouts = workouts;
    }

    public async Task<Result<SessionResponse>> Handle(GetSessionQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<SessionResponse>(scope.Error);
        }

        var session = await _workouts.GetSessionAsync(scope.Value.TenantId, scope.Value.UserId, request.Id, cancellationToken).ConfigureAwait(false);
        return session is null ? Result.Failure<SessionResponse>(FitnessErrors.NotFound) : Result.Success(session);
    }
}

public sealed record CreateSessionCommand(Guid? ProgramId, Guid? DayId, string? Notes) : IRequest<Result<SessionResponse>>;

public sealed record StartSessionCommand(Guid Id) : IRequest<Result<SessionResponse>>;

public sealed record CompleteSessionCommand(Guid Id) : IRequest<Result<SessionResponse>>;

public sealed record CancelSessionCommand(Guid Id) : IRequest<Result<SessionResponse>>;

public sealed class SessionLifecycleHandler :
    IRequestHandler<CreateSessionCommand, Result<SessionResponse>>,
    IRequestHandler<StartSessionCommand, Result<SessionResponse>>,
    IRequestHandler<CompleteSessionCommand, Result<SessionResponse>>,
    IRequestHandler<CancelSessionCommand, Result<SessionResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IWorkoutRepository _workouts;
    private readonly IChallengeRepository _challenges;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public SessionLifecycleHandler(IOperationGuard guard, ICurrentActor actor, IWorkoutRepository workouts, IChallengeRepository challenges, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _workouts = workouts;
        _challenges = challenges;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<SessionResponse>> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<SessionResponse>(scope.Error);
        }

        if (request.ProgramId is Guid programId && !await _workouts.ProgramVisibleAsync(scope.Value.TenantId, scope.Value.UserId, programId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<SessionResponse>(FitnessErrors.NotFound);
        }

        var session = WorkoutSession.Plan(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, request.ProgramId, request.DayId, request.Notes);
        await _workouts.AddSessionAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _workouts.GetSessionAsync(scope.Value.TenantId, scope.Value.UserId, session.Id, cancellationToken).ConfigureAwait(false))!);
    }

    public Task<Result<SessionResponse>> Handle(StartSessionCommand request, CancellationToken cancellationToken) =>
        Transition(request.Id, session => session.Start(_clock.UtcNow), false, cancellationToken);

    public Task<Result<SessionResponse>> Handle(CompleteSessionCommand request, CancellationToken cancellationToken) =>
        Transition(request.Id, session => session.Complete(_clock.UtcNow), true, cancellationToken);

    public Task<Result<SessionResponse>> Handle(CancelSessionCommand request, CancellationToken cancellationToken) =>
        Transition(request.Id, session => session.Cancel(), false, cancellationToken);

    private async Task<Result<SessionResponse>> Transition(Guid id, Func<WorkoutSession, bool> change, bool completed, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<SessionResponse>(scope.Error);
        }

        var session = await _workouts.GetSessionEntityAsync(scope.Value.TenantId, scope.Value.UserId, id, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<SessionResponse>(FitnessErrors.NotFound);
        }

        if (!change(session))
        {
            return Result.Failure<SessionResponse>(FitnessErrors.InvalidState);
        }

        if (completed)
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow);
            var earlier = await _workouts.CompletedCountAsync(scope.Value.TenantId, scope.Value.UserId, today.ToDateTime(TimeOnly.MinValue), cancellationToken).ConfigureAwait(false);
            await _challenges.RecordWorkoutProgressAsync(scope.Value.TenantId, scope.Value.UserId, earlier <= 1, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
            await _challenges.AddNotificationAsync(Notification.Create(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, NotificationType.WorkoutCompleted, "Workout completed", "A workout was recorded.", _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), scope.Value.TenantId, scope.Value.UserId, "workout.completed", "WorkoutSession", session.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _workouts.GetSessionAsync(scope.Value.TenantId, scope.Value.UserId, id, cancellationToken).ConfigureAwait(false))!);
    }
}

public sealed record AddSessionExerciseCommand(Guid SessionId, Guid ExerciseId, string? Notes) : IRequest<Result<SessionResponse>>;

public sealed record AddSetCommand(Guid SessionExerciseId, int? Reps, decimal? Weight, int? DurationSeconds, decimal? Distance, decimal? Rpe, bool Completed) : IRequest<Result<SetResponse>>;

public sealed class TrackingCommandHandler :
    IRequestHandler<AddSessionExerciseCommand, Result<SessionResponse>>,
    IRequestHandler<AddSetCommand, Result<SetResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IFitnessRepository _fitness;
    private readonly IWorkoutRepository _workouts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public TrackingCommandHandler(IOperationGuard guard, ICurrentActor actor, IFitnessRepository fitness, IWorkoutRepository workouts, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _fitness = fitness;
        _workouts = workouts;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<SessionResponse>> Handle(AddSessionExerciseCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<SessionResponse>(scope.Error);
        }

        var session = await _workouts.GetSessionEntityAsync(scope.Value.TenantId, scope.Value.UserId, request.SessionId, cancellationToken).ConfigureAwait(false);
        var exercise = await _fitness.GetExerciseAsync(scope.Value.TenantId, request.ExerciseId, cancellationToken).ConfigureAwait(false);
        if (session is null || exercise is null || session.Status is SessionStatus.Completed or SessionStatus.Cancelled)
        {
            return Result.Failure<SessionResponse>(session is null || exercise is null ? FitnessErrors.NotFound : FitnessErrors.InvalidState);
        }

        var sequence = await _workouts.NextSessionSequenceAsync(session.Id, cancellationToken).ConfigureAwait(false);
        await _workouts.AddSessionExerciseAsync(WorkoutSessionExercise.Create(Guid.NewGuid(), session.Id, exercise.Id, sequence, request.Notes, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _workouts.GetSessionAsync(scope.Value.TenantId, scope.Value.UserId, session.Id, cancellationToken).ConfigureAwait(false))!);
    }

    public async Task<Result<SetResponse>> Handle(AddSetCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<SetResponse>(scope.Error);
        }

        if (request.Rpe is < 0 or > 10)
        {
            return Result.Failure<SetResponse>(FitnessErrors.InvalidState);
        }

        var owned = await _workouts.GetOwnedSessionExerciseAsync(scope.Value.TenantId, scope.Value.UserId, request.SessionExerciseId, cancellationToken).ConfigureAwait(false);
        if (owned is null)
        {
            return Result.Failure<SetResponse>(FitnessErrors.NotFound);
        }

        var number = await _workouts.NextSetNumberAsync(owned.Id, cancellationToken).ConfigureAwait(false);
        var set = WorkoutSet.Record(Guid.NewGuid(), owned.Id, number, request.Reps, request.Weight, request.DurationSeconds, request.Distance, request.Rpe, request.Completed, _clock.UtcNow);
        await _workouts.AddSetAsync(set, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(FitnessMaps.ToResponse(set));
    }
}

public sealed record ListRecordsQuery : IRequest<Result<IReadOnlyList<PersonalRecordResponse>>>;

public sealed class ListRecordsQueryHandler : IRequestHandler<ListRecordsQuery, Result<IReadOnlyList<PersonalRecordResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IWorkoutRepository _workouts;

    public ListRecordsQueryHandler(IOperationGuard guard, ICurrentActor actor, IWorkoutRepository workouts)
    {
        _guard = guard;
        _actor = actor;
        _workouts = workouts;
    }

    public async Task<Result<IReadOnlyList<PersonalRecordResponse>>> Handle(ListRecordsQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.WorkoutRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<PersonalRecordResponse>>(scope.Error);
        }

        var records = await _workouts.ListRecordsAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        return Result.Success(records);
    }
}
