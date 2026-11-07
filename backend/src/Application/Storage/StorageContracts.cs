using LifeFitAI.Domain.Storage;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Storage;

public sealed record StorageConnectionResponse(
    string Provider,
    string Status,
    string? AccountEmail,
    string? RootFolderId,
    DateTime? ConnectedAt,
    DateTime? LastValidatedAt,
    string? LastError,
    bool CanConnect,
    bool StoreProgressPhotos = true,
    string? ProgressFolderId = null,
    string? ProgressFolderName = null);

public sealed record BeginStorageConnectionResponse(string AuthorizationUrl, DateTime ExpiresAt);

public sealed record StoredFileResponse(
    Guid Id,
    string Provider,
    string FileName,
    string ContentType,
    long FileSize,
    string LogicalKey,
    string? EntityType,
    Guid? EntityId,
    string Status,
    DateTime CreatedAt);

public sealed record FileContentResult(byte[] Content, string ContentType, string FileName);

public sealed record FileUploadRequest(
    Guid TenantId,
    Guid ActorUserId,
    string LogicalKey,
    string FileName,
    string ContentType,
    ReadOnlyMemory<byte> Content,
    string? EntityType,
    Guid? EntityId);

public sealed record FileDownloadRequest(Guid TenantId, Guid FileId);

public interface IStorageSecretProtector
{
    string Protect(string plaintext);

    string? Unprotect(string protectedValue);
}

public interface IFileStorageService
{
    Task<Result<StoredFileResponse>> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken);

    Task<Result<FileContentResult>> DownloadAsync(FileDownloadRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid tenantId, Guid fileId, Guid actorUserId, CancellationToken cancellationToken);

    Task<Result<StoredFileResponse>> GetMetadataAsync(Guid tenantId, Guid fileId, CancellationToken cancellationToken);

    Task<Result<bool>> ExistsAsync(Guid tenantId, Guid fileId, CancellationToken cancellationToken);
}

public interface ITenantStorageConnectionRepository
{
    Task<TenantStorageConnection?> GetAsync(Guid tenantId, StorageProviderKind provider, CancellationToken cancellationToken);

    Task AddAsync(TenantStorageConnection connection, CancellationToken cancellationToken);

    Task<StorageOAuthState?> GetOAuthStateByHashAsync(string stateHash, CancellationToken cancellationToken);

    Task AddOAuthStateAsync(StorageOAuthState state, CancellationToken cancellationToken);

    Task<StoredFile?> GetFileAsync(Guid tenantId, Guid fileId, CancellationToken cancellationToken);

    Task<StoredFile?> GetFileByLogicalKeyAsync(Guid tenantId, string logicalKey, CancellationToken cancellationToken);

    Task AddFileAsync(StoredFile file, CancellationToken cancellationToken);
}
