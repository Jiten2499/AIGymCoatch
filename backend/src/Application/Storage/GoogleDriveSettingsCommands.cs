using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Storage;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Storage;

public sealed record GoogleDriveFolderChoice(string Id, string Name, bool Current);

public sealed record ListGoogleDriveFoldersQuery : IRequest<Result<IReadOnlyList<GoogleDriveFolderChoice>>>;

public sealed record UpdateProgressPhotoStorageCommand(bool Enabled) : IRequest<Result<StorageConnectionResponse>>;

public sealed record ChangeGoogleDriveFolderCommand(string FolderId) : IRequest<Result<StorageConnectionResponse>>;

public sealed class ChangeGoogleDriveFolderCommandValidator : AbstractValidator<ChangeGoogleDriveFolderCommand>
{
    public ChangeGoogleDriveFolderCommandValidator() => RuleFor(command => command.FolderId).NotEmpty().MaximumLength(128);
}

public sealed class GoogleDriveSettingsHandler :
    IRequestHandler<ListGoogleDriveFoldersQuery, Result<IReadOnlyList<GoogleDriveFolderChoice>>>,
    IRequestHandler<UpdateProgressPhotoStorageCommand, Result<StorageConnectionResponse>>,
    IRequestHandler<ChangeGoogleDriveFolderCommand, Result<StorageConnectionResponse>>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly ITenantStorageConnectionRepository _storage;
    private readonly IGoogleDriveOAuthService _oauth;
    private readonly IGoogleDriveApiClient _drive;
    private readonly IStorageSecretProtector _secrets;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public GoogleDriveSettingsHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, ITenantStorageConnectionRepository storage, IGoogleDriveOAuthService oauth, IGoogleDriveApiClient drive, IStorageSecretProtector secrets, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _storage = storage;
        _oauth = oauth;
        _drive = drive;
        _secrets = secrets;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<IReadOnlyList<GoogleDriveFolderChoice>>> Handle(ListGoogleDriveFoldersQuery request, CancellationToken cancellationToken)
    {
        var access = await AccessAsync(cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GoogleDriveFolderChoice>>(access.Error);
        }

        if (string.IsNullOrWhiteSpace(access.Value.Connection.RootFolderId))
        {
            return Result.Failure<IReadOnlyList<GoogleDriveFolderChoice>>(StorageErrors.NotConnected);
        }

        var listed = await _drive.ListChildFoldersAsync(access.Value.Token, access.Value.Connection.RootFolderId, cancellationToken).ConfigureAwait(false);
        if (listed.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GoogleDriveFolderChoice>>(listed.Error);
        }

        var current = access.Value.Connection.ProgressFolderId ?? access.Value.Connection.MembersFolderId;
        var choices = listed.Value.Select(folder => new GoogleDriveFolderChoice(folder.Id, folder.Name, folder.Id == current)).ToList();
        if (access.Value.Connection.MembersFolderId is string members && choices.TrueForAll(folder => folder.Id != members))
        {
            choices.Insert(0, new GoogleDriveFolderChoice(members, "Members", members == current));
        }

        return Result.Success<IReadOnlyList<GoogleDriveFolderChoice>>(choices);
    }

    public async Task<Result<StorageConnectionResponse>> Handle(UpdateProgressPhotoStorageCommand request, CancellationToken cancellationToken)
    {
        var access = await AccessAsync(cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<StorageConnectionResponse>(access.Error);
        }

        access.Value.Connection.SetProgressPhotoStorage(request.Enabled);
        await Audit(access.Value.Connection, "GoogleDriveProgressStorageUpdated", request.Enabled ? "enabled" : "disabled", cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await DescribeAsync(access.Value.Connection, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<StorageConnectionResponse>> Handle(ChangeGoogleDriveFolderCommand request, CancellationToken cancellationToken)
    {
        var access = await AccessAsync(cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<StorageConnectionResponse>(access.Error);
        }

        var folder = await _drive.DescribeFolderAsync(access.Value.Token, request.FolderId, cancellationToken).ConfigureAwait(false);
        if (folder.IsFailure)
        {
            return Result.Failure<StorageConnectionResponse>(folder.Error);
        }

        if (!access.Value.Connection.SetProgressFolder(folder.Value.Id, folder.Value.Name))
        {
            return Result.Failure<StorageConnectionResponse>(StorageErrors.InvalidState);
        }

        await Audit(access.Value.Connection, "GoogleDriveFolderChanged", folder.Value.Name, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await DescribeAsync(access.Value.Connection, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Result<(PlatformStorageConnection Connection, string Token)>> AccessAsync(CancellationToken cancellationToken)
    {
        var gate = await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<(PlatformStorageConnection, string)>(gate.Error);
        }

        var connection = await _storage.GetPlatformAsync(StorageProviderKind.GoogleDrive, cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.Status != StorageConnectionStatus.Connected || string.IsNullOrWhiteSpace(connection.ProtectedRefreshToken))
        {
            return Result.Failure<(PlatformStorageConnection, string)>(StorageErrors.NotConnected);
        }

        var refreshToken = _secrets.Unprotect(connection.ProtectedRefreshToken);
        if (refreshToken is null)
        {
            connection.MarkRevoked();
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<(PlatformStorageConnection, string)>(StorageErrors.OAuthRevoked);
        }

        var tokens = await _oauth.RefreshAsync(refreshToken, cancellationToken).ConfigureAwait(false);
        if (tokens.IsFailure)
        {
            connection.MarkRevoked(tokens.Error.Description);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<(PlatformStorageConnection, string)>(StorageErrors.OAuthRevoked);
        }

        return Result.Success((connection, tokens.Value.AccessToken));
    }

    private async Task<StorageConnectionResponse> DescribeAsync(PlatformStorageConnection connection, CancellationToken cancellationToken)
    {
        var canConnect = await _oauth.HasAppAsync(cancellationToken).ConfigureAwait(false);
        return StorageMaps.ToResponse(connection, canConnect, connection.RedirectUri ?? _oauth.RedirectUri);
    }

    private Task Audit(PlatformStorageConnection connection, string action, string? detail, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), _actor.TenantId, _actor.UserId, action, "PlatformStorageConnection", connection.Id, null, detail, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);
}
