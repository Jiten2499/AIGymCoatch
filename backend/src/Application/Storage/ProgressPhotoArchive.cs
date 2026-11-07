using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Storage;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Storage;

public interface IProgressPhotoArchive
{
    Task<Result> SaveAsync(Guid tenantId, Guid actorUserId, Guid assessmentId, Guid photoId, string logicalKey, string contentType, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    Task<Result<byte[]>> OpenAsync(Guid tenantId, string logicalKey, CancellationToken cancellationToken);

    Task DeleteAsync(Guid tenantId, Guid actorUserId, string logicalKey, CancellationToken cancellationToken);
}

public sealed class ProgressPhotoArchive : IProgressPhotoArchive
{
    private readonly IFeatureFlagService _features;
    private readonly ITenantStorageConnectionRepository _connections;
    private readonly IFileStorageService _files;
    private readonly IFileStorage _local;

    public ProgressPhotoArchive(IFeatureFlagService features, ITenantStorageConnectionRepository connections, IFileStorageService files, IFileStorage local)
    {
        _features = features;
        _connections = connections;
        _files = files;
        _local = local;
    }

    public async Task<Result> SaveAsync(Guid tenantId, Guid actorUserId, Guid assessmentId, Guid photoId, string logicalKey, string contentType, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var drive = await DriveTargetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (drive.IsFailure)
        {
            return Result.Failure(drive.Error);
        }

        if (!drive.Value)
        {
            await _local.SaveAsync(logicalKey, content, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        var extension = contentType switch
        {
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "jpg",
        };
        Result<StoredFileResponse> uploaded = Result.Failure<StoredFileResponse>(StorageErrors.UploadFailed);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            uploaded = await _files.UploadAsync(new FileUploadRequest(tenantId, actorUserId, logicalKey, $"{photoId:N}.{extension}", contentType, content, "MemberProgressAssessment", assessmentId), cancellationToken).ConfigureAwait(false);
            if (uploaded.IsSuccess || (uploaded.Error != StorageErrors.UploadFailed && uploaded.Error != StorageErrors.ProviderUnavailable))
            {
                break;
            }
        }

        return uploaded.IsSuccess ? Result.Success() : Result.Failure(uploaded.Error);
    }

    public async Task<Result<byte[]>> OpenAsync(Guid tenantId, string logicalKey, CancellationToken cancellationToken)
    {
        var stored = await _connections.GetFileByLogicalKeyAsync(tenantId, logicalKey, cancellationToken).ConfigureAwait(false);
        if (stored is not null && stored.Status == StoredFileStatus.Active)
        {
            var download = await _files.DownloadAsync(new FileDownloadRequest(tenantId, stored.Id), cancellationToken).ConfigureAwait(false);
            return download.IsSuccess ? Result.Success(download.Value.Content) : Result.Failure<byte[]>(download.Error);
        }

        var local = await _local.ReadAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        return local is null ? Result.Failure<byte[]>(StorageErrors.NotFound) : Result.Success(local);
    }

    public async Task DeleteAsync(Guid tenantId, Guid actorUserId, string logicalKey, CancellationToken cancellationToken)
    {
        var stored = await _connections.GetFileByLogicalKeyAsync(tenantId, logicalKey, cancellationToken).ConfigureAwait(false);
        if (stored is not null && stored.Status == StoredFileStatus.Active)
        {
            await _files.DeleteAsync(tenantId, stored.Id, actorUserId, cancellationToken).ConfigureAwait(false);
        }

        await _local.DeleteAsync(logicalKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<bool>> DriveTargetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!await _features.IsEnabledAsync(tenantId, FeatureCodes.GoogleDriveStorage, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(false);
        }

        var connection = await _connections.GetAsync(tenantId, StorageProviderKind.GoogleDrive, cancellationToken).ConfigureAwait(false);
        if (connection is null || !connection.StoreProgressPhotos || connection.Status == StorageConnectionStatus.NotConnected)
        {
            return Result.Success(false);
        }

        if (connection.Status is StorageConnectionStatus.Revoked or StorageConnectionStatus.Failed)
        {
            return Result.Failure<bool>(connection.Status == StorageConnectionStatus.Revoked ? StorageErrors.OAuthRevoked : StorageErrors.NotConnected);
        }

        return Result.Success(connection.Status == StorageConnectionStatus.Connected);
    }
}
