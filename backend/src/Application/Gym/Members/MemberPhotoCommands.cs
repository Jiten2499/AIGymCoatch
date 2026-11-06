using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record MemberPhotoFile(byte[] Content, string ContentType);

public sealed record UploadMemberPhotoCommand(Guid MemberId, string? ContentType, byte[] Content) : IRequest<Result<MemberResponse>>;

public sealed class UploadMemberPhotoCommandHandler : IRequestHandler<UploadMemberPhotoCommand, Result<MemberResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IFileStorage _storage;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UploadMemberPhotoCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IFileStorage storage, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
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

    public async Task<Result<MemberResponse>> Handle(UploadMemberPhotoCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberUpdate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<MemberResponse>(gate.Error);
        }

        if (!MemberFieldRules.TryContentType(request.Content, request.ContentType, out var contentType))
        {
            return Result.Failure<MemberResponse>(GymErrors.InvalidPhoto);
        }

        var profile = await _people.GetMemberAsync(tenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<MemberResponse>(GymErrors.NotFound);
        }

        var previous = profile.PhotoStorageKey;
        var key = MemberFieldRules.PhotoKey(tenantId, profile.Id, Guid.NewGuid());
        if (!_storage.IsSafeKey(key) || !MemberFieldRules.OwnsPhoto(tenantId, profile.Id, key))
        {
            return Result.Failure<MemberResponse>(GymErrors.InvalidPhoto);
        }

        await _storage.SaveAsync(key, request.Content, cancellationToken).ConfigureAwait(false);
        profile.SetPhoto(key, contentType, request.Content.Length);
        var action = previous is null ? "ProfilePhotoAdded" : "ProfilePhotoReplaced";
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, action, "MemberProfile", profile.Id, null, profile.MemberCode, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (previous is not null && MemberFieldRules.OwnsPhoto(tenantId, profile.Id, previous))
        {
            await _storage.DeleteAsync(previous, cancellationToken).ConfigureAwait(false);
        }

        var member = await _people.ReadMemberAsync(tenantId, profile.Id, cancellationToken).ConfigureAwait(false);
        return member is null ? Result.Failure<MemberResponse>(GymErrors.NotFound) : Result.Success(member);
    }
}

public sealed record RemoveMemberPhotoCommand(Guid MemberId) : IRequest<Result<MemberResponse>>;

public sealed class RemoveMemberPhotoCommandHandler : IRequestHandler<RemoveMemberPhotoCommand, Result<MemberResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IFileStorage _storage;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public RemoveMemberPhotoCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IFileStorage storage, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
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

    public async Task<Result<MemberResponse>> Handle(RemoveMemberPhotoCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberUpdate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<MemberResponse>(gate.Error);
        }

        var profile = await _people.GetMemberAsync(tenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<MemberResponse>(GymErrors.NotFound);
        }

        var previous = profile.PhotoStorageKey;
        profile.ClearPhoto();
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "ProfilePhotoRemoved", "MemberProfile", profile.Id, null, profile.MemberCode, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (MemberFieldRules.OwnsPhoto(tenantId, profile.Id, previous))
        {
            await _storage.DeleteAsync(previous!, cancellationToken).ConfigureAwait(false);
        }

        var member = await _people.ReadMemberAsync(tenantId, profile.Id, cancellationToken).ConfigureAwait(false);
        return member is null ? Result.Failure<MemberResponse>(GymErrors.NotFound) : Result.Success(member);
    }
}

public sealed record GetMemberPhotoQuery(Guid MemberId) : IRequest<Result<MemberPhotoFile>>;

public sealed class GetMemberPhotoQueryHandler : IRequestHandler<GetMemberPhotoQuery, Result<MemberPhotoFile>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IFileStorage _storage;

    public GetMemberPhotoQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IFileStorage storage)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _storage = storage;
    }

    public async Task<Result<MemberPhotoFile>> Handle(GetMemberPhotoQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<MemberPhotoFile>(gate.Error);
        }

        var profile = await _people.GetMemberAsync(tenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        if (profile is null || !MemberFieldRules.OwnsPhoto(tenantId, profile.Id, profile.PhotoStorageKey) || profile.PhotoContentType is null)
        {
            return Result.Failure<MemberPhotoFile>(GymErrors.NotFound);
        }

        var content = await _storage.ReadAsync(profile.PhotoStorageKey!, cancellationToken).ConfigureAwait(false);
        return content is null
            ? Result.Failure<MemberPhotoFile>(GymErrors.NotFound)
            : Result.Success(new MemberPhotoFile(content, profile.PhotoContentType));
    }
}

public sealed record MyMemberPicture(bool HasPhoto);

public sealed record GetMyMemberPictureQuery : IRequest<Result<MyMemberPicture>>;

public sealed class GetMyMemberPictureQueryHandler : IRequestHandler<GetMyMemberPictureQuery, Result<MyMemberPicture>>
{
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;

    public GetMyMemberPictureQueryHandler(ICurrentActor actor, IPeopleRepository people)
    {
        _actor = actor;
        _people = people;
    }

    public async Task<Result<MyMemberPicture>> Handle(GetMyMemberPictureQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId)
        {
            return Result.Failure<MyMemberPicture>(IdentityErrors.Forbidden);
        }

        var profile = await _people.FindMemberWithPhotoAsync(userId, _actor.TenantId, cancellationToken).ConfigureAwait(false);
        var visible = profile is not null && MemberFieldRules.OwnsPhoto(profile.TenantId, profile.Id, profile.PhotoStorageKey);
        return Result.Success(new MyMemberPicture(visible));
    }
}

public sealed record GetMyMemberPhotoQuery : IRequest<Result<MemberPhotoFile>>;

public sealed class GetMyMemberPhotoQueryHandler : IRequestHandler<GetMyMemberPhotoQuery, Result<MemberPhotoFile>>
{
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IFileStorage _storage;

    public GetMyMemberPhotoQueryHandler(ICurrentActor actor, IPeopleRepository people, IFileStorage storage)
    {
        _actor = actor;
        _people = people;
        _storage = storage;
    }

    public async Task<Result<MemberPhotoFile>> Handle(GetMyMemberPhotoQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId)
        {
            return Result.Failure<MemberPhotoFile>(IdentityErrors.Forbidden);
        }

        var profile = await _people.FindMemberWithPhotoAsync(userId, _actor.TenantId, cancellationToken).ConfigureAwait(false);
        if (profile is null || !MemberFieldRules.OwnsPhoto(profile.TenantId, profile.Id, profile.PhotoStorageKey) || profile.PhotoContentType is null)
        {
            return Result.Failure<MemberPhotoFile>(GymErrors.NotFound);
        }

        var content = await _storage.ReadAsync(profile.PhotoStorageKey!, cancellationToken).ConfigureAwait(false);
        return content is null
            ? Result.Failure<MemberPhotoFile>(GymErrors.NotFound)
            : Result.Success(new MemberPhotoFile(content, profile.PhotoContentType));
    }
}
