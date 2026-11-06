using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Communications;

public sealed partial class CommunicationCommandHandler :
    IRequestHandler<CreateCommunicationCommand, Result<CommunicationDetails>>,
    IRequestHandler<UpdateCommunicationCommand, Result<CommunicationDetails>>,
    IRequestHandler<PublishCommunicationCommand, Result<CommunicationDetails>>,
    IRequestHandler<ArchiveCommunicationCommand, Result<CommunicationDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ICommunicationRepository _communications;
    private readonly ICommunicationDelivery _delivery;
    private readonly IPeopleRepository _people;
    private readonly IMembershipRepository _memberships;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;
    private readonly ICommunicationSchedule _schedule;

    public CommunicationCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        ICommunicationRepository communications,
        ICommunicationDelivery delivery,
        IPeopleRepository people,
        IMembershipRepository memberships,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client,
        ICommunicationSchedule schedule)
    {
        _guard = guard;
        _actor = actor;
        _communications = communications;
        _delivery = delivery;
        _people = people;
        _memberships = memberships;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
        _schedule = schedule;
    }

    public async Task<Result<CommunicationDetails>> Handle(CreateCommunicationCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationCreate, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<CommunicationDetails>(gate.Error);
        }

        var audience = await ValidateAudienceAsync(tenantId, request.AudienceType, request.AudiencePackageId, request.AudienceTrainerId, request.SelectedMemberIds, request.SelectedTrainerIds, request.SelectedStaffIds, cancellationToken).ConfigureAwait(false);
        if (audience.IsFailure)
        {
            return Result.Failure<CommunicationDetails>(audience.Error);
        }

        var times = await _schedule.NormalizeAsync(tenantId, request.PublishAt, request.ExpiresAt, request.PublishNow && request.PublishAt is not null, cancellationToken).ConfigureAwait(false);
        if (times.IsFailure)
        {
            return Result.Failure<CommunicationDetails>(times.Error);
        }

        Communication communication;
        try
        {
            communication = Communication.Create(
            Guid.NewGuid(),
            tenantId,
            userId,
            request.Type,
            request.Title,
            request.Message,
            request.Priority,
            request.AudienceType,
            request.AudiencePackageId,
            request.AudienceTrainerId,
            request.ExpiringWithinDays,
            times.Value.PublishAtUtc,
            times.Value.ExpiresAtUtc);
        }
        catch (ArgumentException)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.InvalidState);
        }

        await _communications.AddAsync(communication, cancellationToken).ConfigureAwait(false);
        await StoreAudienceAsync(tenantId, communication.Id, request.AudienceType, request.SelectedMemberIds, request.SelectedTrainerIds, request.SelectedStaffIds, cancellationToken).ConfigureAwait(false);

        if (request.PublishNow)
        {
            var publishGate = await _guard.AllowAsync(PermissionCodes.CommunicationPublish, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
            if (publishGate.IsFailure)
            {
                return Result.Failure<CommunicationDetails>(publishGate.Error);
            }

            communication.Publish(_clock.UtcNow);
            await _delivery.DeliverAsync(communication, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        }

        await _schedule.SyncAsync(communication, cancellationToken).ConfigureAwait(false);
        var auditAction = !request.PublishNow
            ? "CommunicationCreated"
            : communication.Status == CommunicationStatus.Scheduled ? "CommunicationScheduled" : "CommunicationPublished";
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, auditAction, "Communication", communication.Id, null, communication.Title, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _communications.ReadAsync(tenantId, communication.Id, cancellationToken).ConfigureAwait(false))!);
    }

    public async Task<Result<CommunicationDetails>> Handle(UpdateCommunicationCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationUpdate, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<CommunicationDetails>(gate.Error);
        }

        var communication = await _communications.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (communication is null)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.NotFound);
        }

        if (!communication.CanEdit)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.InvalidState);
        }

        var audience = await ValidateAudienceAsync(tenantId, request.AudienceType, request.AudiencePackageId, request.AudienceTrainerId, request.SelectedMemberIds, request.SelectedTrainerIds, request.SelectedStaffIds, cancellationToken).ConfigureAwait(false);
        if (audience.IsFailure)
        {
            return Result.Failure<CommunicationDetails>(audience.Error);
        }

        var times = await _schedule.NormalizeAsync(tenantId, request.PublishAt, request.ExpiresAt, request.PublishNow && request.PublishAt is not null, cancellationToken).ConfigureAwait(false);
        if (times.IsFailure)
        {
            return Result.Failure<CommunicationDetails>(times.Error);
        }

        var wasScheduled = communication.Status == CommunicationStatus.Scheduled;
        var previousPublish = communication.PublishAt;
        try
        {
            communication.UpdateDraft(
                request.Type,
                request.Title,
                request.Message,
                request.Priority,
                request.AudienceType,
                request.AudiencePackageId,
                request.AudienceTrainerId,
                request.ExpiringWithinDays,
                times.Value.PublishAtUtc,
                times.Value.ExpiresAtUtc);
            if (request.PublishNow)
            {
                var publishGate = await _guard.AllowAsync(PermissionCodes.CommunicationPublish, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
                if (publishGate.IsFailure)
                {
                    return Result.Failure<CommunicationDetails>(publishGate.Error);
                }

                communication.Publish(_clock.UtcNow, times.Value.PublishAtUtc is null || times.Value.PublishAtUtc <= _clock.UtcNow);
            }
            else if (wasScheduled)
            {
                communication.ReturnToDraft();
            }
        }
        catch (ArgumentException)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.InvalidState);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.InvalidState);
        }

        await StoreAudienceAsync(tenantId, communication.Id, request.AudienceType, request.SelectedMemberIds, request.SelectedTrainerIds, request.SelectedStaffIds, cancellationToken).ConfigureAwait(false);
        await _schedule.SyncAsync(communication, cancellationToken).ConfigureAwait(false);
        var action = communication.Status == CommunicationStatus.Scheduled
            ? wasScheduled && previousPublish != communication.PublishAt ? "CommunicationScheduleChanged" : "CommunicationScheduled"
            : wasScheduled ? "CommunicationScheduleCancelled" : "CommunicationUpdated";
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, action, "Communication", communication.Id, null, communication.Title, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _communications.ReadAsync(tenantId, communication.Id, cancellationToken).ConfigureAwait(false))!);
    }

    public async Task<Result<CommunicationDetails>> Handle(PublishCommunicationCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationPublish, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<CommunicationDetails>(gate.Error);
        }

        var communication = await _communications.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (communication is null)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.NotFound);
        }

        var wasScheduled = communication.Status == CommunicationStatus.Scheduled;
        try
        {
            communication.Publish(_clock.UtcNow, immediate: true);
            await _delivery.DeliverAsync(communication, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.InvalidState);
        }

        await _schedule.SyncAsync(communication, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, wasScheduled ? "CommunicationPublishedNow" : "CommunicationPublished", "Communication", communication.Id, null, communication.Title, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _communications.ReadAsync(tenantId, communication.Id, cancellationToken).ConfigureAwait(false))!);
    }

    public async Task<Result<CommunicationDetails>> Handle(ArchiveCommunicationCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationArchive, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<CommunicationDetails>(gate.Error);
        }

        var communication = await _communications.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (communication is null)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.NotFound);
        }

        var wasScheduled = communication.Status == CommunicationStatus.Scheduled;
        try
        {
            communication.Archive(_clock.UtcNow);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.InvalidState);
        }

        await _schedule.SyncAsync(communication, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, wasScheduled ? "CommunicationScheduleCancelled" : "CommunicationArchived", "Communication", communication.Id, null, communication.Title, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success((await _communications.ReadAsync(tenantId, communication.Id, cancellationToken).ConfigureAwait(false))!);
    }
}
