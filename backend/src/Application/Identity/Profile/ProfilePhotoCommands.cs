using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Identity.Profile;

public sealed record ProfilePhotoFile(byte[] Content, string ContentType);

public sealed record UploadMyProfilePhotoCommand(string? ContentType, byte[] Content) : IRequest<Result<UserProfile>>;

public sealed record RemoveMyProfilePhotoCommand : IRequest<Result<UserProfile>>;

public sealed record GetProfilePhotoQuery(Guid? UserId) : IRequest<Result<ProfilePhotoFile>>;

public sealed class ProfilePhotoHandler :
    IRequestHandler<UploadMyProfilePhotoCommand, Result<UserProfile>>,
    IRequestHandler<RemoveMyProfilePhotoCommand, Result<UserProfile>>,
    IRequestHandler<GetProfilePhotoQuery, Result<ProfilePhotoFile>>
{
    private readonly ICurrentActor _actor;
    private readonly IUserAccountRepository _users;
    private readonly ITenantMembershipRepository _memberships;
    private readonly IFileStorage _storage;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ProfilePhotoHandler(ICurrentActor actor, IUserAccountRepository users, ITenantMembershipRepository memberships, IFileStorage storage, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _users = users;
        _memberships = memberships;
        _storage = storage;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<UserProfile>> Handle(UploadMyProfilePhotoCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(null, true, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<UserProfile>(opened.Error);
        }

        if (!MemberFieldRules.TryContentType(request.Content, Declared(request.ContentType), out var contentType))
        {
            return Result.Failure<UserProfile>(IdentityErrors.InvalidPhoto);
        }

        var user = opened.Value;
        var previous = user.PhotoStorageKey;
        var key = UserAccount.PhotoKey(user.TenantId, user.Id, Guid.NewGuid());
        if (!_storage.IsSafeKey(key))
        {
            return Result.Failure<UserProfile>(IdentityErrors.InvalidPhoto);
        }

        await _storage.SaveAsync(key, request.Content, cancellationToken).ConfigureAwait(false);
        if (!user.SetPhoto(key, contentType))
        {
            await _storage.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return Result.Failure<UserProfile>(IdentityErrors.InvalidPhoto);
        }
        await Audit(user, previous is null ? "ProfilePhotoAdded" : "ProfilePhotoReplaced", cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await DeleteOwnedAsync(user, previous, cancellationToken).ConfigureAwait(false);
        return Result.Success(UserProfiles.From(user));
    }

    public async Task<Result<UserProfile>> Handle(RemoveMyProfilePhotoCommand request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(null, true, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<UserProfile>(opened.Error);
        }

        var user = opened.Value;
        var previous = user.PhotoStorageKey;
        user.ClearPhoto();
        await Audit(user, "ProfilePhotoRemoved", cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await DeleteOwnedAsync(user, previous, cancellationToken).ConfigureAwait(false);
        return Result.Success(UserProfiles.From(user));
    }

    public async Task<Result<ProfilePhotoFile>> Handle(GetProfilePhotoQuery request, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(request.UserId, false, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<ProfilePhotoFile>(opened.Error);
        }

        var user = opened.Value;
        if (user.PhotoContentType is null || !UserAccount.OwnsPhoto(user.TenantId, user.Id, user.PhotoStorageKey))
        {
            return Result.Failure<ProfilePhotoFile>(IdentityErrors.UserNotFound);
        }

        var content = await _storage.ReadAsync(user.PhotoStorageKey!, cancellationToken).ConfigureAwait(false);
        return content is null
            ? Result.Failure<ProfilePhotoFile>(IdentityErrors.UserNotFound)
            : Result.Success(new ProfilePhotoFile(content, user.PhotoContentType));
    }

    private async Task<Result<UserAccount>> OpenAsync(Guid? requestedUserId, bool writing, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid actorId)
        {
            return Result.Failure<UserAccount>(IdentityErrors.InvalidCredentials);
        }

        var targetId = writing ? actorId : requestedUserId ?? actorId;
        var user = await _users.GetByIdAsync(targetId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure<UserAccount>(IdentityErrors.UserNotFound);
        }

        if (targetId == actorId)
        {
            return Result.Success(user);
        }

        if (_actor.TenantId is not Guid tenantId || await _memberships.GetActiveAsync(tenantId, targetId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<UserAccount>(IdentityErrors.Forbidden);
        }

        return Result.Success(user);
    }

    private Task Audit(UserAccount user, string action, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), _actor.TenantId, user.Id, action, "UserAccount", user.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);

    private async Task DeleteOwnedAsync(UserAccount user, string? key, CancellationToken cancellationToken)
    {
        if (UserAccount.OwnsPhoto(user.TenantId, user.Id, key) && key != user.PhotoStorageKey)
        {
            await _storage.DeleteAsync(key!, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string? Declared(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) || contentType.Equals("image/pjpeg", StringComparison.OrdinalIgnoreCase)
            ? "image/jpeg"
            : contentType;
    }
}
