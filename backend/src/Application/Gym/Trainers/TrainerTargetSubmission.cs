using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record MyTrainerTargetItem(
    Guid Id,
    string Name,
    string Metric,
    decimal GoalValue,
    int AwardPoints,
    string Period,
    DateOnly EffectiveStart,
    DateOnly? EffectiveEnd,
    string Status,
    string? SubmissionStatus,
    string? SubmissionDetails);

public sealed record ListMyTrainerTargetsQuery : IRequest<Result<IReadOnlyList<MyTrainerTargetItem>>>;

public sealed record SubmitMyTrainerTargetCommand(Guid AssignmentId, string Details) : IRequest<Result<TrainerRewardTransactionItem>>;

public sealed class SubmitMyTrainerTargetCommandValidator : AbstractValidator<SubmitMyTrainerTargetCommand>
{
    public SubmitMyTrainerTargetCommandValidator()
    {
        RuleFor(command => command.Details).NotEmpty().MaximumLength(500);
    }
}

public sealed class MyTrainerTargetQueryHandler : IRequestHandler<ListMyTrainerTargetsQuery, Result<IReadOnlyList<MyTrainerTargetItem>>>
{
    private readonly ICurrentActor _actor;
    private readonly ITrainerRewardStore _store;

    public MyTrainerTargetQueryHandler(ICurrentActor actor, ITrainerRewardStore store)
    {
        _actor = actor;
        _store = store;
    }

    public async Task<Result<IReadOnlyList<MyTrainerTargetItem>>> Handle(ListMyTrainerTargetsQuery request, CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<IReadOnlyList<MyTrainerTargetItem>>(IdentityErrors.Forbidden);
        }

        var trainer = await _store.GetTrainerByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (trainer is null)
        {
            return Result.Success<IReadOnlyList<MyTrainerTargetItem>>([]);
        }

        var assignments = await _store.ListAssignmentsAsync(tenantId, trainer.Id, cancellationToken).ConfigureAwait(false);
        var ledger = await _store.ListTransactionsAsync(tenantId, trainer.Id, cancellationToken).ConfigureAwait(false);
        var rows = assignments.Select(item =>
        {
            var award = ledger
                .Where(tx => tx.SourceAssignmentId == item.Id && tx.TransactionType == TrainerRewardTransactionType.Award)
                .OrderByDescending(tx => tx.CreatedAt)
                .FirstOrDefault();
            return new MyTrainerTargetItem(
                item.Id,
                item.SnapshotName,
                item.SnapshotMetric.ToString(),
                item.GoalValue,
                item.SnapshotAwardPoints,
                item.Period.ToString(),
                item.EffectiveStart,
                item.EffectiveEnd,
                item.Status.ToString(),
                award?.ApprovalStatus.ToString(),
                award?.Reason);
        }).ToList();
        return Result.Success<IReadOnlyList<MyTrainerTargetItem>>(rows);
    }
}

public sealed partial class TrainerRewardLedgerHandlers : IRequestHandler<SubmitMyTrainerTargetCommand, Result<TrainerRewardTransactionItem>>
{
    public Task<Result<TrainerRewardTransactionItem>> Handle(SubmitMyTrainerTargetCommand request, CancellationToken cancellationToken) =>
        PostOwnAsync(cancellationToken, async (tenantId, actorId, settings) =>
        {
            var trainer = await _store.GetTrainerByUserAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false);
            var assignment = await _store.GetAssignmentAsync(tenantId, request.AssignmentId, cancellationToken).ConfigureAwait(false);
            if (trainer is null || assignment is null || assignment.TrainerProfileId != trainer.Id)
            {
                return Result.Failure<TrainerRewardTransactionItem>(GymErrors.NotFound);
            }

            if (assignment.Status != TrainerTargetAssignmentStatus.Active)
            {
                return Result.Failure<TrainerRewardTransactionItem>(GymErrors.InvalidState);
            }

            if (await _store.HasOpenAwardAsync(tenantId, assignment.Id, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<TrainerRewardTransactionItem>(GymErrors.Conflict);
            }

            var points = assignment.SnapshotAwardPoints > 0 ? assignment.SnapshotAwardPoints : settings.DefaultAwardPoints;
            var key = $"submit:{assignment.Id}:{Guid.NewGuid():N}";
            var tx = TrainerRewardTransaction.Award(Guid.NewGuid(), tenantId, assignment.TrainerProfileId, points, assignment.Id, key, request.Details.Trim(), requireApproval: true, _clock.UtcNow, actorId);
            return await PersistAsync(tenantId, actorId, tx, "TrainerTargetSubmitted", requireApproval: true, cancellationToken).ConfigureAwait(false);
        });

    private async Task<Result<TrainerRewardTransactionItem>> PostOwnAsync(
        CancellationToken cancellationToken,
        Func<Guid, Guid, TrainerRewardSettings, Task<Result<TrainerRewardTransactionItem>>> action)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<TrainerRewardTransactionItem>(IdentityErrors.Forbidden);
        }

        var settings = await _store.GetOrCreateSettingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return await action(tenantId, actorId, settings).ConfigureAwait(false);
    }
}
