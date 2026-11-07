using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Storage;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Storage;

public sealed class FileStorageService : IFileStorageService
{
    private readonly IFeatureFlagService _features;
    private readonly ITenantStorageConnectionRepository _storage;
    private readonly IGoogleDriveOAuthService _oauth;
    private readonly IGoogleDriveApiClient _drive;
    private readonly IStorageSecretProtector _secrets;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public FileStorageService(
        IFeatureFlagService features,
        ITenantStorageConnectionRepository storage,
        IGoogleDriveOAuthService oauth,
        IGoogleDriveApiClient drive,
        IStorageSecretProtector secrets,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _features = features;
        _storage = storage;
        _oauth = oauth;
        _drive = drive;
        _secrets = secrets;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<StoredFileResponse>> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken)
    {
        if (!await _features.IsEnabledAsync(request.TenantId, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<StoredFileResponse>(StorageErrors.FeatureDisabled);
        }

        if (!StorageKeys.IsSafeLogicalKey(request.TenantId, request.LogicalKey)
            || !StorageKeys.IsAllowedContentType(request.ContentType)
            || !StorageKeys.IsAllowedSize(request.Content.Length))
        {
            return Result.Failure<StoredFileResponse>(StorageKeys.IsSafeLogicalKey(request.TenantId, request.LogicalKey) ? StorageErrors.InvalidFile : StorageErrors.InvalidKey);
        }

        if (await _storage.GetFileByLogicalKeyAsync(request.TenantId, request.LogicalKey, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<StoredFileResponse>(StorageErrors.Conflict);
        }

        var access = await AccessTokenAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<StoredFileResponse>(access.Error);
        }

        var category = StorageKeys.CategoryFromKey(request.LogicalKey) ?? "other";
        var folderId = access.Value.Connection.FolderForCategory(category);
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return Result.Failure<StoredFileResponse>(StorageErrors.NotConnected);
        }

        var fileName = StorageKeys.SanitizeFileName(request.FileName);
        var uploaded = await _drive.UploadAsync(access.Value.AccessToken, folderId, fileName, request.ContentType, request.Content, cancellationToken).ConfigureAwait(false);
        if (uploaded.IsFailure)
        {
            return Result.Failure<StoredFileResponse>(uploaded.Error);
        }

        var file = StoredFile.Create(
            Guid.NewGuid(),
            request.TenantId,
            StorageProviderKind.GoogleDrive,
            uploaded.Value.ExternalFileId,
            uploaded.Value.ParentFolderId,
            fileName,
            request.ContentType,
            request.Content.Length,
            request.LogicalKey,
            request.EntityType,
            request.EntityId,
            request.ActorUserId);
        await _storage.AddFileAsync(file, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), request.TenantId, request.ActorUserId, "StoredFileUploaded", "StoredFile", file.Id, null, file.LogicalKey, null, null, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(StorageMaps.ToResponse(file));
    }

    public async Task<Result<FileContentResult>> DownloadAsync(FileDownloadRequest request, CancellationToken cancellationToken)
    {
        if (!await _features.IsEnabledAsync(request.TenantId, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<FileContentResult>(StorageErrors.FeatureDisabled);
        }

        var file = await _storage.GetFileAsync(request.TenantId, request.FileId, cancellationToken).ConfigureAwait(false);
        if (file is null || file.Status != StoredFileStatus.Active)
        {
            return Result.Failure<FileContentResult>(StorageErrors.NotFound);
        }

        var access = await AccessTokenAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<FileContentResult>(access.Error);
        }

        var content = await _drive.DownloadAsync(access.Value.AccessToken, file.ExternalFileId, cancellationToken).ConfigureAwait(false);
        return content.IsFailure
            ? Result.Failure<FileContentResult>(content.Error)
            : Result.Success(new FileContentResult(content.Value, file.ContentType, file.FileName));
    }

    public async Task<Result> DeleteAsync(Guid tenantId, Guid fileId, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!await _features.IsEnabledAsync(tenantId, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(StorageErrors.FeatureDisabled);
        }

        var file = await _storage.GetFileAsync(tenantId, fileId, cancellationToken).ConfigureAwait(false);
        if (file is null || file.Status != StoredFileStatus.Active)
        {
            return Result.Failure(StorageErrors.NotFound);
        }

        var access = await AccessTokenAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var deleted = await _drive.DeleteAsync(access.Value.AccessToken, file.ExternalFileId, cancellationToken).ConfigureAwait(false);
        if (deleted.IsFailure)
        {
            return Result.Failure(deleted.Error);
        }

        file.MarkDeleted();
        file.SoftDelete(_clock.UtcNow);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorUserId, "StoredFileDeleted", "StoredFile", file.Id, null, file.LogicalKey, null, null, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<StoredFileResponse>> GetMetadataAsync(Guid tenantId, Guid fileId, CancellationToken cancellationToken)
    {
        if (!await _features.IsEnabledAsync(tenantId, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<StoredFileResponse>(StorageErrors.FeatureDisabled);
        }

        var file = await _storage.GetFileAsync(tenantId, fileId, cancellationToken).ConfigureAwait(false);
        return file is null || file.Status != StoredFileStatus.Active
            ? Result.Failure<StoredFileResponse>(StorageErrors.NotFound)
            : Result.Success(StorageMaps.ToResponse(file));
    }

    public async Task<Result<bool>> ExistsAsync(Guid tenantId, Guid fileId, CancellationToken cancellationToken)
    {
        if (!await _features.IsEnabledAsync(tenantId, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(StorageErrors.FeatureDisabled);
        }

        var file = await _storage.GetFileAsync(tenantId, fileId, cancellationToken).ConfigureAwait(false);
        return Result.Success(file is not null && file.Status == StoredFileStatus.Active);
    }

    private async Task<Result<(TenantStorageConnection Connection, string AccessToken)>> AccessTokenAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var connection = await _storage.GetAsync(tenantId, StorageProviderKind.GoogleDrive, cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.Status != StorageConnectionStatus.Connected || string.IsNullOrWhiteSpace(connection.ProtectedRefreshToken))
        {
            return Result.Failure<(TenantStorageConnection, string)>(StorageErrors.NotConnected);
        }

        var refreshToken = _secrets.Unprotect(connection.ProtectedRefreshToken);
        if (refreshToken is null)
        {
            connection.MarkRevoked();
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<(TenantStorageConnection, string)>(StorageErrors.OAuthRevoked);
        }

        var tokens = await _oauth.RefreshAsync(refreshToken, cancellationToken).ConfigureAwait(false);
        if (tokens.IsFailure)
        {
            connection.MarkRevoked(tokens.Error.Description);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<(TenantStorageConnection, string)>(StorageErrors.OAuthRevoked);
        }

        return Result.Success((connection, tokens.Value.AccessToken));
    }
}
