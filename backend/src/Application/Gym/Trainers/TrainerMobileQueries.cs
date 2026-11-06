using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed partial class TrainerMobileQueryHandlers :
    IRequestHandler<GetTrainerHomeQuery, Result<TrainerHomeItem>>,
    IRequestHandler<GetMyTrainerScheduleQuery, Result<IReadOnlyList<TrainerSessionItem>>>,
    IRequestHandler<GetMyTrainerSessionQuery, Result<TrainerSessionDetailItem>>,
    IRequestHandler<GetMyTrainerProfileQuery, Result<MyTrainerProfileItem>>,
    IRequestHandler<ListMyTrainerAlertsQuery, Result<IReadOnlyList<TrainerAlertItem>>>,
    IRequestHandler<ListMyTrainerMembersQuery, Result<IReadOnlyList<TrainerMemberItem>>>,
    IRequestHandler<GetMyTrainerMemberQuery, Result<TrainerMemberDetailItem>>,
    IRequestHandler<GetMyTrainerPerformanceQuery, Result<MyTrainerPerformanceItem>>
{
    private readonly ICurrentActor _actor;
    private readonly ITrainerMobileStore _store;
    private readonly IDateTimeProvider _clock;

    public TrainerMobileQueryHandlers(ICurrentActor actor, ITrainerMobileStore store, IDateTimeProvider clock)
    {
        _actor = actor;
        _store = store;
        _clock = clock;
    }

    public async Task<Result<TrainerHomeItem>> Handle(GetTrainerHomeQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return Result.Failure<TrainerHomeItem>(IdentityErrors.Forbidden);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var sessions = await Sessions(scope.Value, today.AddDays(-14), today.AddDays(2), cancellationToken).ConfigureAwait(false);
        var todayRows = sessions.Where(item => DateOnly.FromDateTime(item.ScheduledStartUtc) == today).ToArray();
        var next = todayRows.Where(item => item.Status == nameof(PtSessionStatus.Scheduled) && item.ScheduledStartUtc >= _clock.UtcNow).MinBy(item => item.ScheduledStartUtc)
            ?? todayRows.Where(item => item.Status == nameof(PtSessionStatus.Scheduled)).MinBy(item => item.ScheduledStartUtc);
        var members = await _store.ListAssignedMembersAsync(scope.Value.TenantId, scope.Value.TrainerId, cancellationToken).ConfigureAwait(false);
        var assignments = await _store.RecentAssignmentsAsync(scope.Value.TenantId, scope.Value.TrainerId, today.AddDays(-7), cancellationToken).ConfigureAwait(false);
        var pending = sessions.Count(item => item.Status == nameof(PtSessionStatus.Scheduled) && item.ScheduledEndUtc < _clock.UtcNow);
        return Result.Success(new TrainerHomeItem(scope.Value.FirstName, todayRows.Length, members.Count, pending, assignments.Count, next, todayRows, scope.Value.HasPhoto));
    }

    public async Task<Result<IReadOnlyList<TrainerSessionItem>>> Handle(GetMyTrainerScheduleQuery request, CancellationToken cancellationToken)
    {
        if (request.To < request.From || request.To.DayNumber - request.From.DayNumber > 62)
        {
            return Result.Failure<IReadOnlyList<TrainerSessionItem>>(GymErrors.InvalidState);
        }

        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return Result.Failure<IReadOnlyList<TrainerSessionItem>>(IdentityErrors.Forbidden);
        }

        return Result.Success(await Sessions(scope.Value, request.From, request.To.AddDays(1), cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<TrainerSessionDetailItem>> Handle(GetMyTrainerSessionQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return Result.Failure<TrainerSessionDetailItem>(IdentityErrors.Forbidden);
        }

        var session = await _store.GetSessionAsync(scope.Value.TenantId, scope.Value.TrainerId, request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<TrainerSessionDetailItem>(GymErrors.NotFound);
        }

        var mapped = (await Map([session], scope.Value, cancellationToken).ConfigureAwait(false)).Single();
        var exercises = await _store.ListExercisesAsync(scope.Value.TenantId, session.Id, cancellationToken).ConfigureAwait(false);
        var rows = exercises.Select(item => new TrainerExerciseItem(item.Id, item.Name, item.MuscleGroup, item.Sets, item.Reps, item.Completed, item.SortOrder)).ToArray();
        return Result.Success(new TrainerSessionDetailItem(mapped, rows));
    }

    public async Task<Result<MyTrainerProfileItem>> Handle(GetMyTrainerProfileQuery request, CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<MyTrainerProfileItem>(IdentityErrors.Forbidden);
        }

        var user = await _store.GetUserNameAsync(userId, cancellationToken).ConfigureAwait(false);
        var trainer = await _store.GetTrainerByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (user is null || trainer is null)
        {
            return Result.Failure<MyTrainerProfileItem>(GymErrors.NotFound);
        }

        return Result.Success(new MyTrainerProfileItem(user.Value.FirstName, user.Value.LastName, user.Value.Email, trainer.Specialization, trainer.Certification, trainer.Bio, trainer.ExperienceYears, trainer.JoiningDate, trainer.PhotoStorageKey is not null));
    }

    private async Task<TrainerScope?> Scope(CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return null;
        }

        var trainer = await _store.GetTrainerByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (trainer is null)
        {
            return null;
        }

        var user = await _store.GetUserNameAsync(userId, cancellationToken).ConfigureAwait(false);
        return new TrainerScope(tenantId, trainer.Id, user?.FirstName ?? string.Empty, trainer.PhotoStorageKey is not null);
    }

    private async Task<IReadOnlyList<TrainerSessionItem>> Sessions(TrainerScope scope, DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken)
    {
        var rows = await _store.ListSessionsAsync(scope.TenantId, scope.TrainerId, TrainerMobileMap.StartOf(from), TrainerMobileMap.StartOf(toExclusive), cancellationToken).ConfigureAwait(false);
        return await Map(rows, scope, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<TrainerSessionItem>> Map(IReadOnlyList<PtSession> rows, TrainerScope scope, CancellationToken cancellationToken)
    {
        var names = await _store.MemberNamesAsync(scope.TenantId, rows.Select(item => item.MemberProfileId).Distinct().ToArray(), cancellationToken).ConfigureAwait(false);
        var day = DateOnly.FromDateTime(_clock.UtcNow);
        var result = new List<TrainerSessionItem>(rows.Count);
        foreach (var row in rows)
        {
            var packages = await _store.ListMemberPackagesAsync(scope.TenantId, row.MemberProfileId, cancellationToken).ConfigureAwait(false);
            names.TryGetValue(row.MemberProfileId, out var name);
            result.Add(TrainerMobileMap.Session(row, name ?? string.Empty, TrainerMobileMap.CurrentPackage(packages, day)));
        }

        return result;
    }

    private readonly record struct TrainerScope(Guid TenantId, Guid TrainerId, string FirstName, bool HasPhoto);
}
