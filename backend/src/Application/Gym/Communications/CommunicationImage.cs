using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Communications;

public sealed record CommunicationImageFile(byte[] Content, string ContentType);

public sealed record AttachCommunicationImageCommand(Guid Id, string? ContentType, byte[] Content) : IRequest<Result<CommunicationDetails>>;

public sealed record GetCommunicationImageQuery(Guid Id) : IRequest<Result<CommunicationImageFile>>;

public sealed class CommunicationImageHandler :
    IRequestHandler<AttachCommunicationImageCommand, Result<CommunicationDetails>>,
    IRequestHandler<GetCommunicationImageQuery, Result<CommunicationImageFile>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ICommunicationRepository _communications;
    private readonly IFileStorage _storage;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CommunicationImageHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        ICommunicationRepository communications,
        IFileStorage storage,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _communications = communications;
        _storage = storage;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<CommunicationDetails>> Handle(AttachCommunicationImageCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationUpdate, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<CommunicationDetails>(gate.Error);
        }

        if (!MemberFieldRules.TryContentType(request.Content, request.ContentType, out var contentType))
        {
            return Result.Failure<CommunicationDetails>(GymErrors.InvalidPhoto);
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

        var previous = communication.ImageStorageKey;
        var key = $"tenants/{tenantId:D}/communications/{communication.Id:D}/{Guid.NewGuid():D}";
        if (!_storage.IsSafeKey(key))
        {
            return Result.Failure<CommunicationDetails>(GymErrors.InvalidPhoto);
        }

        await _storage.SaveAsync(key, request.Content, cancellationToken).ConfigureAwait(false);
        communication.SetImage(key, contentType);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "CommunicationImageAttached", "Communication", communication.Id, null, communication.Title, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (previous is not null && previous.StartsWith($"tenants/{tenantId:D}/communications/{communication.Id:D}/", StringComparison.Ordinal))
        {
            await _storage.DeleteAsync(previous, cancellationToken).ConfigureAwait(false);
        }

        var details = await _communications.ReadAsync(tenantId, communication.Id, cancellationToken).ConfigureAwait(false);
        return details is null ? Result.Failure<CommunicationDetails>(GymErrors.NotFound) : Result.Success(details);
    }

    public async Task<Result<CommunicationImageFile>> Handle(GetCommunicationImageQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationRead, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<CommunicationImageFile>(gate.Error);
        }

        var communication = await _communications.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (communication?.ImageStorageKey is not string key)
        {
            return Result.Failure<CommunicationImageFile>(GymErrors.NotFound);
        }

        var recipient = await _communications.GetRecipientAsync(tenantId, userId, communication.Id, cancellationToken).ConfigureAwait(false);
        var canManage = communication.CreatedByUserId == userId
            || (await _guard.AllowAsync(PermissionCodes.CommunicationUpdate, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false)).IsSuccess;
        if (recipient is null && !canManage)
        {
            return Result.Failure<CommunicationImageFile>(GymErrors.NotFound);
        }

        var content = await _storage.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        return content is null
            ? Result.Failure<CommunicationImageFile>(GymErrors.NotFound)
            : Result.Success(new CommunicationImageFile(content, communication.ImageContentType ?? "application/octet-stream"));
    }
}
