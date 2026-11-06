using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record ListTrainerWorkspaceQuery : IRequest<Result<IReadOnlyList<TrainerListItem>>>;

public sealed record ListTrainerCandidatesQuery : IRequest<Result<IReadOnlyList<TrainerCandidate>>>;

public sealed record GetTrainerWorkspaceQuery(Guid TrainerId, DateOnly From, DateOnly To) : IRequest<Result<TrainerDetails>>;

public sealed record CreateTrainerProfileCommand(Guid UserId, string? EmployeeCode, string? Specialization, string? Bio, DateOnly? JoiningDate, bool CommissionEligible) : IRequest<Result<Guid>>;

public sealed record UpdateTrainerEmploymentCommand(Guid TrainerId, string? EmployeeCode, string? Specialization, string? Bio, DateOnly? JoiningDate, bool CommissionEligible, ProfileStatus Status) : IRequest<Result>;

public sealed record AssignTrainerMemberCommand(Guid TrainerId, Guid MemberId, DateOnly StartDate) : IRequest<Result>;

public sealed record EndTrainerAssignmentCommand(Guid AssignmentId, DateOnly EndDate) : IRequest<Result>;

public sealed record ChangeTrainerAssignmentCommand(Guid AssignmentId, Guid TrainerId, DateOnly StartDate) : IRequest<Result>;

public sealed record ListTrainerTargetsQuery(Guid TrainerId, DateOnly From, DateOnly To) : IRequest<Result<IReadOnlyList<TrainerTargetItem>>>;

public sealed record CreateTrainerTargetCommand(Guid TrainerId, TrainerTargetType TargetType, TrainerTargetPeriod Period, DateOnly Start, DateOnly End, decimal TargetValue, string? Name = null) : IRequest<Result>;

public sealed record SaveTrainerIncentiveRuleCommand(TrainerTargetType TargetType, decimal Percent) : IRequest<Result>;

public sealed record GetTrainerPerformanceQuery(DateOnly From, DateOnly To) : IRequest<Result<IReadOnlyList<TrainerPerformanceRow>>>;

public sealed class TrainerWorkspaceHandlers :
    IRequestHandler<ListTrainerWorkspaceQuery, Result<IReadOnlyList<TrainerListItem>>>,
    IRequestHandler<ListTrainerCandidatesQuery, Result<IReadOnlyList<TrainerCandidate>>>,
    IRequestHandler<GetTrainerWorkspaceQuery, Result<TrainerDetails>>,
    IRequestHandler<CreateTrainerProfileCommand, Result<Guid>>,
    IRequestHandler<UpdateTrainerEmploymentCommand, Result>,
    IRequestHandler<AssignTrainerMemberCommand, Result>,
    IRequestHandler<EndTrainerAssignmentCommand, Result>,
    IRequestHandler<ChangeTrainerAssignmentCommand, Result>,
    IRequestHandler<ListTrainerTargetsQuery, Result<IReadOnlyList<TrainerTargetItem>>>,
    IRequestHandler<CreateTrainerTargetCommand, Result>,
    IRequestHandler<SaveTrainerIncentiveRuleCommand, Result>,
    IRequestHandler<GetTrainerPerformanceQuery, Result<IReadOnlyList<TrainerPerformanceRow>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ITrainerWorkspace _trainers;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public TrainerWorkspaceHandlers(IOperationGuard guard, ICurrentActor actor, ITrainerWorkspace trainers, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _trainers = trainers;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public Task<Result<IReadOnlyList<TrainerListItem>>> Handle(ListTrainerWorkspaceQuery request, CancellationToken cancellationToken) =>
        Read(async (tenantId, token) => Result.Success(await _trainers.ListAsync(tenantId, token).ConfigureAwait(false)), cancellationToken);

    public Task<Result<IReadOnlyList<TrainerCandidate>>> Handle(ListTrainerCandidatesQuery request, CancellationToken cancellationToken) =>
        Read(async (tenantId, token) => Result.Success(await _trainers.CandidatesAsync(tenantId, token).ConfigureAwait(false)), cancellationToken);

    public Task<Result<TrainerDetails>> Handle(GetTrainerWorkspaceQuery request, CancellationToken cancellationToken) =>
        Read(async (tenantId, token) =>
        {
            var details = await _trainers.DetailsAsync(tenantId, request.TrainerId, request.From, request.To, token).ConfigureAwait(false);
            return details is null ? Result.Failure<TrainerDetails>(GymErrors.NotFound) : Result.Success(details);
        }, cancellationToken);

    public Task<Result<Guid>> Handle(CreateTrainerProfileCommand request, CancellationToken cancellationToken) =>
        Change(PermissionCodes.StaffCreate, "TrainerProfileCreated", async (tenantId, actorId, token) =>
        {
            var profile = await _trainers.CreateFromUserAsync(tenantId, request.UserId, request.EmployeeCode, request.Specialization, request.Bio, request.JoiningDate, request.CommissionEligible, token).ConfigureAwait(false);
            return profile is null ? Result.Failure<Guid>(GymErrors.Conflict) : Result.Success(profile.Id);
        }, cancellationToken);

    public Task<Result> Handle(UpdateTrainerEmploymentCommand request, CancellationToken cancellationToken) =>
        Change(PermissionCodes.TrainerUpdate, "TrainerProfileUpdated", async (tenantId, _, token) =>
            await _trainers.UpdateEmploymentAsync(tenantId, request.TrainerId, request.EmployeeCode, request.Specialization, request.Bio, request.JoiningDate, request.CommissionEligible, request.Status, token).ConfigureAwait(false)
                ? Result.Success()
                : Result.Failure(GymErrors.NotFound), cancellationToken);

    public Task<Result> Handle(AssignTrainerMemberCommand request, CancellationToken cancellationToken) =>
        Change(PermissionCodes.TrainerUpdate, "TrainerMemberAssigned", async (tenantId, actorId, token) =>
            await _trainers.AssignAsync(tenantId, request.TrainerId, request.MemberId, request.StartDate, actorId, token).ConfigureAwait(false) is null
                ? Result.Failure(GymErrors.NotFound)
                : Result.Success(), cancellationToken);

    public Task<Result> Handle(EndTrainerAssignmentCommand request, CancellationToken cancellationToken) =>
        Change(PermissionCodes.TrainerUpdate, "TrainerMemberUnassigned", async (tenantId, _, token) =>
            await _trainers.EndAssignmentAsync(tenantId, request.AssignmentId, request.EndDate, token).ConfigureAwait(false) ? Result.Success() : Result.Failure(GymErrors.NotFound), cancellationToken);

    public Task<Result> Handle(ChangeTrainerAssignmentCommand request, CancellationToken cancellationToken) =>
        Change(PermissionCodes.TrainerUpdate, "TrainerMemberReassigned", async (tenantId, actorId, token) =>
            await _trainers.ChangeTrainerAsync(tenantId, request.AssignmentId, request.TrainerId, request.StartDate, actorId, token).ConfigureAwait(false) is null
                ? Result.Failure(GymErrors.NotFound)
                : Result.Success(), cancellationToken);

    public Task<Result<IReadOnlyList<TrainerTargetItem>>> Handle(ListTrainerTargetsQuery request, CancellationToken cancellationToken) =>
        Read(async (tenantId, token) => Result.Success(await _trainers.TargetsAsync(tenantId, request.TrainerId, request.From, request.To, token).ConfigureAwait(false)), cancellationToken);

    public Task<Result> Handle(CreateTrainerTargetCommand request, CancellationToken cancellationToken) =>
        Change(PermissionCodes.TrainerUpdate, "TrainerTargetCreated", async (tenantId, _, token) =>
            await _trainers.AddTargetAsync(tenantId, request.TrainerId, request.TargetType, request.Period, request.Start, request.End, request.TargetValue, token, request.Name).ConfigureAwait(false) is null
                ? Result.Failure(GymErrors.InvalidState)
                : Result.Success(), cancellationToken);

    public Task<Result> Handle(SaveTrainerIncentiveRuleCommand request, CancellationToken cancellationToken) =>
        Change(PermissionCodes.TrainerUpdate, "TrainerIncentiveRuleSaved", async (tenantId, _, token) =>
        {
            if (request.Percent < 0 || request.Percent > 100)
            {
                return Result.Failure(GymErrors.InvalidState);
            }

            await _trainers.SaveIncentiveRuleAsync(tenantId, request.TargetType, request.Percent, token).ConfigureAwait(false);
            return Result.Success();
        }, cancellationToken);

    public Task<Result<IReadOnlyList<TrainerPerformanceRow>>> Handle(GetTrainerPerformanceQuery request, CancellationToken cancellationToken) =>
        Read(async (tenantId, token) => Result.Success(await _trainers.PerformanceAsync(tenantId, request.From, request.To, token).ConfigureAwait(false)), cancellationToken);

    private async Task<Result<T>> Read<T>(Func<Guid, CancellationToken, Task<Result<T>>> action, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerRead, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure || _actor.TenantId is not Guid tenantId
            ? Result.Failure<T>(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden)
            : await action(tenantId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> Change(string permission, string audit, Func<Guid, Guid, CancellationToken, Task<Result>> action, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(permission, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden);
        }

        var result = await action(tenantId, actorId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, audit, "TrainerProfile", null, null, audit, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<Result<T>> Change<T>(string permission, string audit, Func<Guid, Guid, CancellationToken, Task<Result<T>>> action, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(permission, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<T>(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden);
        }

        var result = await action(tenantId, actorId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, audit, "TrainerProfile", null, null, audit, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
