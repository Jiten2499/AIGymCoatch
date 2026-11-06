using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record GetCoachWorkspaceQuery(Guid TrainerId, DateOnly From, DateOnly To) : IRequest<Result<CoachWorkspace>>;

public sealed record SetTrainerTargetActiveCommand(Guid TargetId, bool Active) : IRequest<Result>;

public sealed record CreateCommissionRuleCommand(Guid TrainerId, string Name, CommissionRuleKind Kind, decimal? Percent, decimal? FixedAmount, DateOnly Start, DateOnly? End, bool RequiresApproval) : IRequest<Result>;

public sealed record CreatePerformanceIncentiveCommand(Guid TrainerId, string Name, TrainerTargetType Metric, decimal Threshold, IncentiveRewardKind RewardKind, decimal? FixedAmount, decimal? Percent, DateOnly Start, DateOnly? End, bool RequiresApproval) : IRequest<Result>;

public sealed record CalculateCoachCompensationCommand(Guid TrainerId, DateOnly From, DateOnly To) : IRequest<Result<int>>;

public sealed record DecideCoachCompensationCommand(Guid CalculationId, CompensationStatus Status) : IRequest<Result>;

public sealed class TrainerCompensationHandlers :
    IRequestHandler<GetCoachWorkspaceQuery, Result<CoachWorkspace>>,
    IRequestHandler<SetTrainerTargetActiveCommand, Result>,
    IRequestHandler<CreateCommissionRuleCommand, Result>,
    IRequestHandler<CreatePerformanceIncentiveCommand, Result>,
    IRequestHandler<CalculateCoachCompensationCommand, Result<int>>,
    IRequestHandler<DecideCoachCompensationCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ITrainerWorkspace _trainers;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public TrainerCompensationHandlers(IOperationGuard guard, ICurrentActor actor, ITrainerWorkspace trainers, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _trainers = trainers;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public Task<Result<CoachWorkspace>> Handle(GetCoachWorkspaceQuery request, CancellationToken cancellationToken) =>
        Read(async (tenantId, token) =>
        {
            var coach = await _trainers.CoachAsync(tenantId, request.TrainerId, request.From, request.To, token).ConfigureAwait(false);
            return coach is null ? Result.Failure<CoachWorkspace>(GymErrors.NotFound) : Result.Success(coach);
        }, cancellationToken);

    public Task<Result> Handle(SetTrainerTargetActiveCommand request, CancellationToken cancellationToken) =>
        Change(request.TargetId, "TrainerTargetUpdated", async (tenantId, token) =>
            await _trainers.SetTargetActiveAsync(tenantId, request.TargetId, request.Active, token).ConfigureAwait(false) ? Result.Success() : Result.Failure(GymErrors.NotFound), cancellationToken);

    public Task<Result> Handle(CreateCommissionRuleCommand request, CancellationToken cancellationToken) =>
        Change(request.TrainerId, "TrainerCommissionRuleSaved", async (tenantId, token) =>
            await _trainers.AddCommissionRuleAsync(tenantId, request.TrainerId, request.Name, request.Kind, request.Percent, request.FixedAmount, request.Start, request.End, request.RequiresApproval, token).ConfigureAwait(false) is null
                ? Result.Failure(GymErrors.InvalidState)
                : Result.Success(), cancellationToken);

    public Task<Result> Handle(CreatePerformanceIncentiveCommand request, CancellationToken cancellationToken) =>
        Change(request.TrainerId, "TrainerIncentiveRuleSaved", async (tenantId, token) =>
            await _trainers.AddPerformanceIncentiveAsync(tenantId, request.TrainerId, request.Name, request.Metric, request.Threshold, request.RewardKind, request.FixedAmount, request.Percent, request.Start, request.End, request.RequiresApproval, token).ConfigureAwait(false) is null
                ? Result.Failure(GymErrors.InvalidState)
                : Result.Success(), cancellationToken);

    public async Task<Result<int>> Handle(CalculateCoachCompensationCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerUpdate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<int>(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden);
        }

        var created = await _trainers.CalculateCompensationAsync(tenantId, request.TrainerId, request.From, request.To, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "TrainerCompensationCalculated", "TrainerProfile", request.TrainerId, null, created.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(created);
    }

    public Task<Result> Handle(DecideCoachCompensationCommand request, CancellationToken cancellationToken) =>
        Change(request.CalculationId, "TrainerCompensationDecided", async (tenantId, token) =>
            await _trainers.DecideCalculationAsync(tenantId, request.CalculationId, request.Status, token).ConfigureAwait(false) is null
                ? Result.Failure(GymErrors.InvalidState)
                : Result.Success(), cancellationToken);

    private async Task<Result<T>> Read<T>(Func<Guid, CancellationToken, Task<Result<T>>> action, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerRead, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure || _actor.TenantId is not Guid tenantId
            ? Result.Failure<T>(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden)
            : await action(tenantId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> Change(Guid entityId, string audit, Func<Guid, CancellationToken, Task<Result>> action, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerUpdate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure(gate.IsFailure ? gate.Error : IdentityErrors.Forbidden);
        }

        var result = await action(tenantId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, audit, "TrainerProfile", entityId, null, audit, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
