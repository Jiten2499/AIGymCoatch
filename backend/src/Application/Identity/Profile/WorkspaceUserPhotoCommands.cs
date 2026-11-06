using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Platform;
using LifeFitAI.Application.Tenancy;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Identity.Profile;

public sealed record UploadWorkspaceUserPhotoCommand(Guid UserId, string? ContentType, byte[] Content) : IRequest<Result>;

public sealed class UploadWorkspaceUserPhotoHandler : IRequestHandler<UploadWorkspaceUserPhotoCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly IUserAccountRepository _users;
    private readonly ITenantMembershipRepository _memberships;
    private readonly IFileStorage _storage;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UploadWorkspaceUserPhotoHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        IUserAccountRepository users,
        ITenantMembershipRepository memberships,
        IFileStorage storage,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _users = users;
        _memberships = memberships;
        _storage = storage;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(UploadWorkspaceUserPhotoCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.UserCreate, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || await _memberships.GetActiveAsync(access.Value.TenantId, user.Id, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        if (!MemberFieldRules.TryContentType(request.Content, Declared(request.ContentType), out var contentType))
        {
            return Result.Failure(IdentityErrors.InvalidPhoto);
        }

        var previous = user.PhotoStorageKey;
        var key = UserAccount.PhotoKey(user.TenantId, user.Id, Guid.NewGuid());
        if (!_storage.IsSafeKey(key))
        {
            return Result.Failure(IdentityErrors.InvalidPhoto);
        }

        await _storage.SaveAsync(key, request.Content, cancellationToken).ConfigureAwait(false);
        if (!user.SetPhoto(key, contentType))
        {
            await _storage.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return Result.Failure(IdentityErrors.InvalidPhoto);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), access.Value.TenantId, access.Value.ActorId, previous is null ? "ProfilePhotoAdded" : "ProfilePhotoReplaced", "UserAccount", user.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (UserAccount.OwnsPhoto(user.TenantId, user.Id, previous) && previous != user.PhotoStorageKey)
        {
            await _storage.DeleteAsync(previous!, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
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
