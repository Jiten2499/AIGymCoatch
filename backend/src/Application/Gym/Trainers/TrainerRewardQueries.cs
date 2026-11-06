using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record GetTrainerRewardSettingsQuery : IRequest<Result<TrainerRewardSettingsDto>>;

public sealed record ListTrainerTargetDefinitionsQuery : IRequest<Result<IReadOnlyList<TrainerTargetDefinitionItem>>>;

public sealed record ListTrainerTargetAssignmentsQuery(Guid? TrainerId) : IRequest<Result<IReadOnlyList<TrainerTargetAssignmentItem>>>;

public sealed record ListTrainerRewardTransactionsQuery(Guid TrainerId) : IRequest<Result<IReadOnlyList<TrainerRewardTransactionItem>>>;

public sealed record GetTrainerRewardBalanceQuery(Guid TrainerId) : IRequest<Result<TrainerRewardBalanceItem>>;

public sealed class TrainerRewardQueryHandlers :
    IRequestHandler<GetTrainerRewardSettingsQuery, Result<TrainerRewardSettingsDto>>,
    IRequestHandler<ListTrainerTargetDefinitionsQuery, Result<IReadOnlyList<TrainerTargetDefinitionItem>>>,
    IRequestHandler<ListTrainerTargetAssignmentsQuery, Result<IReadOnlyList<TrainerTargetAssignmentItem>>>,
    IRequestHandler<ListTrainerRewardTransactionsQuery, Result<IReadOnlyList<TrainerRewardTransactionItem>>>,
    IRequestHandler<GetTrainerRewardBalanceQuery, Result<TrainerRewardBalanceItem>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ITrainerRewardStore _store;
    private readonly IUnitOfWork _unitOfWork;

    public TrainerRewardQueryHandlers(IOperationGuard guard, ICurrentActor actor, ITrainerRewardStore store, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TrainerRewardSettingsDto>> Handle(GetTrainerRewardSettingsQuery request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<TrainerRewardSettingsDto>(denied.Error);
        }

        var settings = await _store.GetOrCreateSettingsAsync(_actor.TenantId!.Value, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new TrainerRewardSettingsDto(settings.PointsEnabled, settings.DefaultAwardPoints, settings.RequireApprovalForAwards, settings.AllowManualAdjustments));
    }

    public async Task<Result<IReadOnlyList<TrainerTargetDefinitionItem>>> Handle(ListTrainerTargetDefinitionsQuery request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<IReadOnlyList<TrainerTargetDefinitionItem>>(denied.Error);
        }

        var rows = await _store.ListDefinitionsAsync(_actor.TenantId!.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<TrainerTargetDefinitionItem>>(rows.Select(Map).ToList());
    }

    public async Task<Result<IReadOnlyList<TrainerTargetAssignmentItem>>> Handle(ListTrainerTargetAssignmentsQuery request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<IReadOnlyList<TrainerTargetAssignmentItem>>(denied.Error);
        }

        var tenantId = _actor.TenantId!.Value;
        var rows = await _store.ListAssignmentsAsync(tenantId, request.TrainerId, cancellationToken).ConfigureAwait(false);
        var items = new List<TrainerTargetAssignmentItem>();
        foreach (var row in rows)
        {
            var name = await _store.TrainerNameAsync(tenantId, row.TrainerProfileId, cancellationToken).ConfigureAwait(false) ?? "Coach";
            items.Add(new TrainerTargetAssignmentItem(
                row.Id,
                row.TrainerProfileId,
                name,
                row.TargetDefinitionId,
                row.SnapshotName,
                row.SnapshotMetric.ToString(),
                row.Period.ToString(),
                row.GoalValue,
                row.SnapshotAwardPoints,
                row.SnapshotRewardRule,
                row.EffectiveStart,
                row.EffectiveEnd,
                row.Status.ToString()));
        }

        return Result.Success<IReadOnlyList<TrainerTargetAssignmentItem>>(items);
    }

    public async Task<Result<IReadOnlyList<TrainerRewardTransactionItem>>> Handle(ListTrainerRewardTransactionsQuery request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<IReadOnlyList<TrainerRewardTransactionItem>>(denied.Error);
        }

        var rows = await _store.ListTransactionsAsync(_actor.TenantId!.Value, request.TrainerId, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<TrainerRewardTransactionItem>>(rows.Select(Map).ToList());
    }

    public async Task<Result<TrainerRewardBalanceItem>> Handle(GetTrainerRewardBalanceQuery request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<TrainerRewardBalanceItem>(denied.Error);
        }

        var tenantId = _actor.TenantId!.Value;
        var trainer = await _store.GetTrainerAsync(tenantId, request.TrainerId, cancellationToken).ConfigureAwait(false);
        if (trainer is null)
        {
            return Result.Failure<TrainerRewardBalanceItem>(GymErrors.NotFound);
        }

        var name = await _store.TrainerNameAsync(tenantId, request.TrainerId, cancellationToken).ConfigureAwait(false) ?? "Coach";
        var balance = await _store.BalanceAsync(tenantId, request.TrainerId, cancellationToken).ConfigureAwait(false);
        return Result.Success(new TrainerRewardBalanceItem(request.TrainerId, name, balance));
    }

    private Task<Result> Gate(CancellationToken cancellationToken) =>
        _guard.AllowAsync(PermissionCodes.TrainerRead, FeatureCodes.StaffManagement, cancellationToken);

    private static TrainerTargetDefinitionItem Map(Domain.Gym.TrainerTargetDefinition item) =>
        new(item.Id, item.Name, item.Description, item.Metric.ToString(), item.DefaultGoal, item.Period.ToString(), item.DefaultAwardPoints, item.Status.ToString());

    private static TrainerRewardTransactionItem Map(Domain.Gym.TrainerRewardTransaction item) =>
        new(item.Id, item.TrainerProfileId, item.Points, item.TransactionType.ToString(), item.ApprovalStatus.ToString(), item.SourceAssignmentId, item.SourceTransactionId, item.IdempotencyKey, item.Reason, item.CreatedAt, item.PostedAt);
}
