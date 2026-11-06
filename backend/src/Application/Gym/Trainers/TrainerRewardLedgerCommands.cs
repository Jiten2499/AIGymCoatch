using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record AwardTrainerRewardCommand(Guid AssignmentId, int? Points, string IdempotencyKey, string? Reason) : IRequest<Result<TrainerRewardTransactionItem>>;

public sealed record AdjustTrainerRewardCommand(Guid TrainerId, int Points, string IdempotencyKey, string? Reason) : IRequest<Result<TrainerRewardTransactionItem>>;

public sealed record ReverseTrainerRewardCommand(Guid TransactionId, string IdempotencyKey, string? Reason) : IRequest<Result<TrainerRewardTransactionItem>>;

public sealed class AwardTrainerRewardCommandValidator : AbstractValidator<AwardTrainerRewardCommand>
{
    public AwardTrainerRewardCommandValidator()
    {
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Reason).MaximumLength(500);
        RuleFor(command => command.Points).GreaterThan(0).When(command => command.Points is not null);
    }
}

public sealed class AdjustTrainerRewardCommandValidator : AbstractValidator<AdjustTrainerRewardCommand>
{
    public AdjustTrainerRewardCommandValidator()
    {
        RuleFor(command => command.Points).NotEqual(0);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Reason).MaximumLength(500);
    }
}

public sealed class ReverseTrainerRewardCommandValidator : AbstractValidator<ReverseTrainerRewardCommand>
{
    public ReverseTrainerRewardCommandValidator()
    {
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Reason).MaximumLength(500);
    }
}

public sealed partial class TrainerRewardLedgerHandlers :
    IRequestHandler<AwardTrainerRewardCommand, Result<TrainerRewardTransactionItem>>,
    IRequestHandler<AdjustTrainerRewardCommand, Result<TrainerRewardTransactionItem>>,
    IRequestHandler<ReverseTrainerRewardCommand, Result<TrainerRewardTransactionItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ITrainerRewardStore _store;
    private readonly IApprovalRequestRepository _approvals;
    private readonly IUserAccountRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public TrainerRewardLedgerHandlers(
        IOperationGuard guard,
        ICurrentActor actor,
        ITrainerRewardStore store,
        IApprovalRequestRepository approvals,
        IUserAccountRepository users,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _approvals = approvals;
        _users = users;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public Task<Result<TrainerRewardTransactionItem>> Handle(AwardTrainerRewardCommand request, CancellationToken cancellationToken) =>
        PostAsync(cancellationToken, async (tenantId, actorId, settings) =>
        {
            if (await _store.FindByIdempotencyAsync(tenantId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false) is { } existing)
            {
                return Result.Success(Map(existing));
            }

            var assignment = await _store.GetAssignmentAsync(tenantId, request.AssignmentId, cancellationToken).ConfigureAwait(false);
            if (assignment is null || assignment.Status != TrainerTargetAssignmentStatus.Active)
            {
                return Result.Failure<TrainerRewardTransactionItem>(assignment is null ? GymErrors.NotFound : GymErrors.InvalidState);
            }

            if (await _store.HasOpenAwardAsync(tenantId, assignment.Id, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<TrainerRewardTransactionItem>(GymErrors.Conflict);
            }

            var points = request.Points ?? (assignment.SnapshotAwardPoints > 0 ? assignment.SnapshotAwardPoints : settings.DefaultAwardPoints);
            var tx = TrainerRewardTransaction.Award(Guid.NewGuid(), tenantId, assignment.TrainerProfileId, points, assignment.Id, request.IdempotencyKey, request.Reason, settings.RequireApprovalForAwards, _clock.UtcNow, actorId);
            var posted = await PersistAsync(tenantId, actorId, tx, "TrainerRewardAwarded", settings.RequireApprovalForAwards, cancellationToken).ConfigureAwait(false);
            if (posted.IsSuccess && tx is not null && tx.ApprovalStatus == TrainerRewardApprovalStatus.Approved)
            {
                assignment.Complete();
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return posted;
        });

    public Task<Result<TrainerRewardTransactionItem>> Handle(AdjustTrainerRewardCommand request, CancellationToken cancellationToken) =>
        PostAsync(cancellationToken, async (tenantId, actorId, settings) =>
        {
            if (!settings.AllowManualAdjustments)
            {
                return Result.Failure<TrainerRewardTransactionItem>(GymErrors.InvalidState);
            }

            if (await _store.GetTrainerAsync(tenantId, request.TrainerId, cancellationToken).ConfigureAwait(false) is null)
            {
                return Result.Failure<TrainerRewardTransactionItem>(GymErrors.NotFound);
            }

            if (await _store.FindByIdempotencyAsync(tenantId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false) is { } existing)
            {
                return Result.Success(Map(existing));
            }

            var tx = TrainerRewardTransaction.Adjust(Guid.NewGuid(), tenantId, request.TrainerId, request.Points, request.IdempotencyKey, request.Reason, settings.RequireApprovalForAwards, _clock.UtcNow, actorId);
            return await PersistAsync(tenantId, actorId, tx, "TrainerRewardAdjusted", settings.RequireApprovalForAwards, cancellationToken).ConfigureAwait(false);
        });

    public Task<Result<TrainerRewardTransactionItem>> Handle(ReverseTrainerRewardCommand request, CancellationToken cancellationToken) =>
        PostAsync(cancellationToken, async (tenantId, actorId, _) =>
        {
            var source = await _store.GetTransactionAsync(tenantId, request.TransactionId, cancellationToken).ConfigureAwait(false);
            if (source is null)
            {
                return Result.Failure<TrainerRewardTransactionItem>(GymErrors.NotFound);
            }

            if (await _store.FindByIdempotencyAsync(tenantId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false) is { } existing)
            {
                return Result.Success(Map(existing));
            }

            var tx = TrainerRewardTransaction.Reverse(Guid.NewGuid(), source, request.IdempotencyKey, request.Reason, _clock.UtcNow, actorId);
            return await PersistAsync(tenantId, actorId, tx, "TrainerRewardReversed", requireApproval: false, cancellationToken).ConfigureAwait(false);
        });

    private async Task<Result<TrainerRewardTransactionItem>> PostAsync(
        CancellationToken cancellationToken,
        Func<Guid, Guid, TrainerRewardSettings, Task<Result<TrainerRewardTransactionItem>>> action)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<TrainerRewardTransactionItem>(IdentityErrors.Forbidden);
        }

        var gate = await _guard.AllowAsync(PermissionCodes.TrainerUpdate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<TrainerRewardTransactionItem>(gate.Error);
        }

        var settings = await _store.GetOrCreateSettingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!settings.PointsEnabled)
        {
            return Result.Failure<TrainerRewardTransactionItem>(GymErrors.FeatureDisabled);
        }

        return await action(tenantId, actorId, settings).ConfigureAwait(false);
    }

    private async Task<Result<TrainerRewardTransactionItem>> PersistAsync(
        Guid tenantId,
        Guid actorId,
        TrainerRewardTransaction? tx,
        string auditAction,
        bool requireApproval,
        CancellationToken cancellationToken)
    {
        if (tx is null)
        {
            return Result.Failure<TrainerRewardTransactionItem>(GymErrors.InvalidState);
        }

        await _store.AddTransactionAsync(tx, cancellationToken).ConfigureAwait(false);
        if (requireApproval && tx.ApprovalStatus == TrainerRewardApprovalStatus.Pending)
        {
            var name = await DisplayNameAsync(actorId, cancellationToken).ConfigureAwait(false);
            await _approvals.AddAsync(
                ApprovalRequest.Create(
                    Guid.NewGuid(),
                    tenantId,
                    ApprovalRequestType.TrainerRewardAward,
                    tx.Id,
                    PermissionCodes.TrainerUpdate,
                    "Coach reward points",
                    $"{tx.Points} recognition points ({tx.TransactionType})",
                    tx.Reason,
                    actorId,
                    name,
                    null,
                    "Coach recognition",
                    null,
                    null,
                    null,
                    tx.Points,
                    null),
                cancellationToken).ConfigureAwait(false);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, auditAction, "TrainerRewardTransaction", tx.Id, null, tx.ApprovalStatus.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(tx));
    }

    private async Task<string> DisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null ? "Staff" : $"{user.FirstName} {user.LastName}".Trim();
    }

    private static TrainerRewardTransactionItem Map(TrainerRewardTransaction item) =>
        new(item.Id, item.TrainerProfileId, item.Points, item.TransactionType.ToString(), item.ApprovalStatus.ToString(), item.SourceAssignmentId, item.SourceTransactionId, item.IdempotencyKey, item.Reason, item.CreatedAt, item.PostedAt);
}
