using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record UpdateTrainerRewardSettingsCommand(bool PointsEnabled, int DefaultAwardPoints, bool RequireApprovalForAwards, bool AllowManualAdjustments) : IRequest<Result<TrainerRewardSettingsDto>>;

public sealed record CreateTrainerTargetDefinitionCommand(string Name, string? Description, TrainerTargetType Metric, decimal DefaultGoal, TrainerTargetPeriod Period, int DefaultAwardPoints, string? Status) : IRequest<Result<TrainerTargetDefinitionItem>>;

public sealed record UpdateTrainerTargetDefinitionCommand(Guid Id, string Name, string? Description, TrainerTargetType Metric, decimal DefaultGoal, TrainerTargetPeriod Period, int DefaultAwardPoints) : IRequest<Result<TrainerTargetDefinitionItem>>;

public sealed record SetTrainerTargetDefinitionStatusCommand(Guid Id, TrainerTargetDefinitionStatus Status) : IRequest<Result>;

public sealed record AssignTrainerTargetCommand(Guid TrainerId, Guid DefinitionId, decimal? GoalValue, DateOnly EffectiveStart, DateOnly? EffectiveEnd) : IRequest<Result<TrainerTargetAssignmentItem>>;

public sealed record CompleteTrainerTargetAssignmentCommand(Guid AssignmentId) : IRequest<Result>;

public sealed class UpdateTrainerRewardSettingsCommandValidator : AbstractValidator<UpdateTrainerRewardSettingsCommand>
{
    public UpdateTrainerRewardSettingsCommandValidator() => RuleFor(command => command.DefaultAwardPoints).GreaterThanOrEqualTo(0);
}

public sealed class CreateTrainerTargetDefinitionCommandValidator : AbstractValidator<CreateTrainerTargetDefinitionCommand>
{
    public CreateTrainerTargetDefinitionCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.DefaultGoal).GreaterThan(0);
        RuleFor(command => command.DefaultAwardPoints).GreaterThanOrEqualTo(0);
    }
}

public sealed class TrainerRewardCommandHandlers :
    IRequestHandler<UpdateTrainerRewardSettingsCommand, Result<TrainerRewardSettingsDto>>,
    IRequestHandler<CreateTrainerTargetDefinitionCommand, Result<TrainerTargetDefinitionItem>>,
    IRequestHandler<UpdateTrainerTargetDefinitionCommand, Result<TrainerTargetDefinitionItem>>,
    IRequestHandler<SetTrainerTargetDefinitionStatusCommand, Result>,
    IRequestHandler<AssignTrainerTargetCommand, Result<TrainerTargetAssignmentItem>>,
    IRequestHandler<CompleteTrainerTargetAssignmentCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ITrainerRewardStore _store;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public TrainerRewardCommandHandlers(IOperationGuard guard, ICurrentActor actor, ITrainerRewardStore store, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _store = store;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<TrainerRewardSettingsDto>> Handle(UpdateTrainerRewardSettingsCommand request, CancellationToken cancellationToken)
    {
        var access = await WriteAccess(cancellationToken).ConfigureAwait(false);
        if (access.Error is not null)
        {
            return Result.Failure<TrainerRewardSettingsDto>(access.Error);
        }

        var settings = await _store.GetOrCreateSettingsAsync(access.TenantId, cancellationToken).ConfigureAwait(false);
        settings.Update(request.PointsEnabled, request.DefaultAwardPoints, request.RequireApprovalForAwards, request.AllowManualAdjustments);
        await _store.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        await Audit(access, "TrainerRewardSettingsUpdated", "TrainerRewardSettings", settings.Id, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new TrainerRewardSettingsDto(settings.PointsEnabled, settings.DefaultAwardPoints, settings.RequireApprovalForAwards, settings.AllowManualAdjustments));
    }

    public async Task<Result<TrainerTargetDefinitionItem>> Handle(CreateTrainerTargetDefinitionCommand request, CancellationToken cancellationToken)
    {
        var access = await WriteAccess(cancellationToken).ConfigureAwait(false);
        if (access.Error is not null)
        {
            return Result.Failure<TrainerTargetDefinitionItem>(access.Error);
        }

        var status = ParseCreateStatus(request.Status);
        if (status is null || await _store.DefinitionNameExistsAsync(access.TenantId, request.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TrainerTargetDefinitionItem>(status is null ? GymErrors.InvalidState : GymErrors.Conflict);
        }

        var definition = TrainerTargetDefinition.Create(Guid.NewGuid(), access.TenantId, request.Name, request.Description, request.Metric, request.DefaultGoal, request.Period, request.DefaultAwardPoints, status.Value);
        if (definition is null)
        {
            return Result.Failure<TrainerTargetDefinitionItem>(GymErrors.InvalidState);
        }

        await _store.AddDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        await Audit(access, "TrainerTargetDefinitionCreated", "TrainerTargetDefinition", definition.Id, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(definition));
    }

    public async Task<Result<TrainerTargetDefinitionItem>> Handle(UpdateTrainerTargetDefinitionCommand request, CancellationToken cancellationToken)
    {
        var access = await WriteAccess(cancellationToken).ConfigureAwait(false);
        if (access.Error is not null)
        {
            return Result.Failure<TrainerTargetDefinitionItem>(access.Error);
        }

        var definition = await _store.GetDefinitionAsync(access.TenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return Result.Failure<TrainerTargetDefinitionItem>(GymErrors.NotFound);
        }

        if (await _store.DefinitionNameExistsAsync(access.TenantId, request.Name, request.Id, cancellationToken).ConfigureAwait(false)
            || !definition.Update(request.Name, request.Description, request.Metric, request.DefaultGoal, request.Period, request.DefaultAwardPoints))
        {
            return Result.Failure<TrainerTargetDefinitionItem>(GymErrors.Conflict);
        }

        await Audit(access, "TrainerTargetDefinitionUpdated", "TrainerTargetDefinition", definition.Id, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(definition));
    }

    public async Task<Result> Handle(SetTrainerTargetDefinitionStatusCommand request, CancellationToken cancellationToken)
    {
        var access = await WriteAccess(cancellationToken).ConfigureAwait(false);
        if (access.Error is not null)
        {
            return Result.Failure(access.Error);
        }

        var definition = await _store.GetDefinitionAsync(access.TenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (definition is null || !definition.SetStatus(request.Status))
        {
            return Result.Failure(definition is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        await Audit(access, "TrainerTargetDefinitionStatusChanged", "TrainerTargetDefinition", definition.Id, request.Status.ToString(), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<TrainerTargetAssignmentItem>> Handle(AssignTrainerTargetCommand request, CancellationToken cancellationToken)
    {
        var access = await WriteAccess(cancellationToken).ConfigureAwait(false);
        if (access.Error is not null)
        {
            return Result.Failure<TrainerTargetAssignmentItem>(access.Error);
        }

        var definition = await _store.GetDefinitionAsync(access.TenantId, request.DefinitionId, cancellationToken).ConfigureAwait(false);
        var trainer = await _store.GetTrainerAsync(access.TenantId, request.TrainerId, cancellationToken).ConfigureAwait(false);
        if (definition is null || trainer is null)
        {
            return Result.Failure<TrainerTargetAssignmentItem>(GymErrors.NotFound);
        }

        await CloseAwardedAssignmentsAsync(access.TenantId, request.TrainerId, request.DefinitionId, cancellationToken).ConfigureAwait(false);

        if (await _store.HasOpenAssignmentAsync(access.TenantId, request.TrainerId, request.DefinitionId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TrainerTargetAssignmentItem>(GymErrors.Conflict);
        }

        var assignment = TrainerTargetAssignment.Open(Guid.NewGuid(), access.TenantId, request.TrainerId, definition, request.GoalValue, request.EffectiveStart, request.EffectiveEnd);
        if (assignment is null)
        {
            return Result.Failure<TrainerTargetAssignmentItem>(GymErrors.InvalidState);
        }

        await _store.AddAssignmentAsync(assignment, cancellationToken).ConfigureAwait(false);
        await Audit(access, "TrainerTargetAssigned", "TrainerTargetAssignment", assignment.Id, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var name = await _store.TrainerNameAsync(access.TenantId, request.TrainerId, cancellationToken).ConfigureAwait(false) ?? "Coach";
        return Result.Success(Map(assignment, name));
    }

    public async Task<Result> Handle(CompleteTrainerTargetAssignmentCommand request, CancellationToken cancellationToken)
    {
        var access = await WriteAccess(cancellationToken).ConfigureAwait(false);
        if (access.Error is not null)
        {
            return Result.Failure(access.Error);
        }

        var assignment = await _store.GetAssignmentAsync(access.TenantId, request.AssignmentId, cancellationToken).ConfigureAwait(false);
        if (assignment is null || !assignment.Complete())
        {
            return Result.Failure(assignment is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        await Audit(access, "TrainerTargetAssignmentCompleted", "TrainerTargetAssignment", assignment.Id, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task CloseAwardedAssignmentsAsync(Guid tenantId, Guid trainerId, Guid definitionId, CancellationToken cancellationToken)
    {
        var open = await _store.ListOpenAssignmentsForUpdateAsync(tenantId, trainerId, definitionId, cancellationToken).ConfigureAwait(false);
        if (open.Count == 0)
        {
            return;
        }

        var ledger = await _store.ListTransactionsAsync(tenantId, trainerId, cancellationToken).ConfigureAwait(false);
        var closed = false;
        foreach (var assignment in open)
        {
            var awards = ledger.Where(item =>
                item.SourceAssignmentId == assignment.Id
                && item.TransactionType == TrainerRewardTransactionType.Award).ToList();
            var hasApproved = awards.Any(item => item.ApprovalStatus == TrainerRewardApprovalStatus.Approved);
            var hasPending = awards.Any(item => item.ApprovalStatus == TrainerRewardApprovalStatus.Pending);
            var hasRejected = awards.Any(item => item.ApprovalStatus == TrainerRewardApprovalStatus.Rejected);

            if (hasApproved && assignment.Complete())
            {
                closed = true;
            }
            else if (!hasPending && hasRejected && assignment.Cancel())
            {
                closed = true;
            }
        }

        if (closed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(Guid TenantId, Guid ActorId, Error? Error)> WriteAccess(CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return (Guid.Empty, Guid.Empty, IdentityErrors.Forbidden);
        }

        var gate = await _guard.AllowAsync(PermissionCodes.TrainerUpdate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure ? (tenantId, actorId, gate.Error) : (tenantId, actorId, null);
    }

    private Task Audit((Guid TenantId, Guid ActorId, Error? Error) access, string action, string entity, Guid entityId, string? after, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), access.TenantId, access.ActorId, action, entity, entityId, null, after, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);

    private static TrainerTargetDefinitionStatus? ParseCreateStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) || status.Equals("Draft", StringComparison.OrdinalIgnoreCase)
            ? TrainerTargetDefinitionStatus.Draft
            : status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                ? TrainerTargetDefinitionStatus.Active
                : null;

    private static TrainerTargetDefinitionItem Map(TrainerTargetDefinition item) =>
        new(item.Id, item.Name, item.Description, item.Metric.ToString(), item.DefaultGoal, item.Period.ToString(), item.DefaultAwardPoints, item.Status.ToString());

    private static TrainerTargetAssignmentItem Map(TrainerTargetAssignment item, string trainerName) =>
        new(item.Id, item.TrainerProfileId, trainerName, item.TargetDefinitionId, item.SnapshotName, item.SnapshotMetric.ToString(), item.Period.ToString(), item.GoalValue, item.SnapshotAwardPoints, item.SnapshotRewardRule, item.EffectiveStart, item.EffectiveEnd, item.Status.ToString());
}
