using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record TrainerPhotoFile(byte[] Content, string ContentType);

public sealed record UploadTrainerPhotoCommand(Guid TrainerId, string? ContentType, byte[] Content) : IRequest<Result>;

public sealed record RemoveTrainerPhotoCommand(Guid TrainerId) : IRequest<Result>;

public sealed record GetTrainerPhotoQuery(Guid TrainerId) : IRequest<Result<TrainerPhotoFile>>;

public sealed record GetMyTrainerPhotoQuery : IRequest<Result<TrainerPhotoFile>>;

public sealed class TrainerPhotoHandler :
    IRequestHandler<UploadTrainerPhotoCommand, Result>,
    IRequestHandler<RemoveTrainerPhotoCommand, Result>,
    IRequestHandler<GetTrainerPhotoQuery, Result<TrainerPhotoFile>>,
    IRequestHandler<GetMyTrainerPhotoQuery, Result<TrainerPhotoFile>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IFileStorage _storage;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public TrainerPhotoHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IFileStorage storage, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _storage = storage;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(UploadTrainerPhotoCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(request.TrainerId, PermissionCodes.TrainerUpdate, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure(opened.Error);
        }

        if (!MemberFieldRules.TryContentType(request.Content, request.ContentType, out var contentType))
        {
            return Result.Failure(GymErrors.InvalidPhoto);
        }

        var trainer = opened.Value;
        var previous = trainer.PhotoStorageKey;
        var key = PhotoKey(trainer.TenantId, trainer.Id, Guid.NewGuid());
        if (!_storage.IsSafeKey(key) || !Owns(trainer.TenantId, trainer.Id, key))
        {
            return Result.Failure(GymErrors.InvalidPhoto);
        }

        await _storage.SaveAsync(key, request.Content, cancellationToken).ConfigureAwait(false);
        trainer.SetPhoto(key, contentType);
        await Audit(trainer, previous is null ? "TrainerPhotoAdded" : "TrainerPhotoReplaced", cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (Owns(trainer.TenantId, trainer.Id, previous))
        {
            await _storage.DeleteAsync(previous!, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> Handle(RemoveTrainerPhotoCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(request.TrainerId, PermissionCodes.TrainerUpdate, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure(opened.Error);
        }

        var trainer = opened.Value;
        var previous = trainer.PhotoStorageKey;
        trainer.ClearPhoto();
        await Audit(trainer, "TrainerPhotoRemoved", cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (Owns(trainer.TenantId, trainer.Id, previous))
        {
            await _storage.DeleteAsync(previous!, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result<TrainerPhotoFile>> Handle(GetMyTrainerPhotoQuery request, CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<TrainerPhotoFile>(IdentityErrors.Forbidden);
        }

        var trainer = await _people.GetTrainerByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (trainer is null)
        {
            return Result.Failure<TrainerPhotoFile>(GymErrors.NotFound);
        }

        return await ReadAsync(trainer, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TrainerPhotoFile>> Handle(GetTrainerPhotoQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(request.TrainerId, PermissionCodes.TrainerRead, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure || opened.Value.PhotoContentType is null || !Owns(opened.Value.TenantId, opened.Value.Id, opened.Value.PhotoStorageKey))
        {
            return Result.Failure<TrainerPhotoFile>(opened.IsFailure ? opened.Error : GymErrors.NotFound);
        }

        return await ReadAsync(opened.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<TrainerPhotoFile>> ReadAsync(TrainerProfile trainer, CancellationToken cancellationToken)
    {
        if (trainer.PhotoContentType is null || !Owns(trainer.TenantId, trainer.Id, trainer.PhotoStorageKey))
        {
            return Result.Failure<TrainerPhotoFile>(GymErrors.NotFound);
        }

        var content = await _storage.ReadAsync(trainer.PhotoStorageKey!, cancellationToken).ConfigureAwait(false);
        return content is null
            ? Result.Failure<TrainerPhotoFile>(GymErrors.NotFound)
            : Result.Success(new TrainerPhotoFile(content, trainer.PhotoContentType));
    }

    private async Task<Result<TrainerProfile>> OpenAsync(Guid trainerId, string permission, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(permission, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<TrainerProfile>(gate.Error);
        }

        var trainer = await _people.GetTrainerAsync(tenantId, trainerId, cancellationToken).ConfigureAwait(false);
        return trainer is null ? Result.Failure<TrainerProfile>(GymErrors.NotFound) : Result.Success(trainer);
    }

    private Task Audit(TrainerProfile trainer, string action, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), trainer.TenantId, _actor.UserId, action, "TrainerProfile", trainer.Id, null, trainer.EmployeeCode, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);

    private static string PhotoKey(Guid tenantId, Guid trainerId, Guid photoId) =>
        $"tenants/{tenantId:D}/trainers/{trainerId:D}/profile/{photoId:D}";

    private static bool Owns(Guid tenantId, Guid trainerId, string? key) =>
        key is not null && key.StartsWith($"tenants/{tenantId:D}/trainers/{trainerId:D}/profile/", StringComparison.Ordinal);
}
