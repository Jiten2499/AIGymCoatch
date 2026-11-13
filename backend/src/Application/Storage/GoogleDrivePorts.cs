using LifeFitAI.Domain.Storage;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Storage;

public sealed record GoogleTokenSet(string AccessToken, string? RefreshToken, string? Scope, int ExpiresInSeconds);

public sealed record GoogleDriveFolderIds(
    string RootFolderId,
    string MembersFolderId,
    string CommunicationsFolderId,
    string GymFolderId,
    string OtherFolderId);

public sealed record GoogleDriveFolder(string Id, string Name);

public sealed record GoogleDriveUploadedFile(string ExternalFileId, string ParentFolderId, string FileName, long FileSize);

public interface IGoogleDriveOAuthService
{
    bool IsConfigured { get; }

    string RedirectUri { get; }

    string WebSuccessRedirectUri { get; }

    string MobileSuccessRedirectUri { get; }

    Task<bool> HasAppAsync(CancellationToken cancellationToken);

    string BuildAuthorizationUrl(string state, string codeChallenge);

    Task<Result<GoogleTokenSet>> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken);

    Task<Result<GoogleTokenSet>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}

public interface IGoogleDriveApiClient
{
    Task<Result<(string Email, string? DisplayName)>> GetAccountAsync(string accessToken, CancellationToken cancellationToken);

    Task<Result<GoogleDriveFolderIds>> EnsureLifeFitFoldersAsync(string accessToken, CancellationToken cancellationToken);

    Task<Result> ValidateFolderAsync(string accessToken, string folderId, CancellationToken cancellationToken);

    Task<Result<GoogleDriveFolder>> DescribeFolderAsync(string accessToken, string folderId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<GoogleDriveFolder>>> ListChildFoldersAsync(string accessToken, string parentId, CancellationToken cancellationToken);

    Task<Result<GoogleDriveUploadedFile>> UploadAsync(
        string accessToken,
        string parentFolderId,
        string fileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    Task<Result<byte[]>> DownloadAsync(string accessToken, string externalFileId, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(string accessToken, string externalFileId, CancellationToken cancellationToken);

    Task<Result<(string FileName, string ContentType, long FileSize)>> GetMetadataAsync(
        string accessToken,
        string externalFileId,
        CancellationToken cancellationToken);
}

public static class StorageMaps
{
    public static StorageConnectionResponse ToResponse(PlatformStorageConnection? connection, bool canConnect, string? defaultRedirect = null) =>
        connection is null
            ? new StorageConnectionResponse(
                StorageProviderKind.GoogleDrive.ToString(),
                StorageConnectionStatus.NotConnected.ToString(),
                null,
                null,
                null,
                null,
                null,
                canConnect,
                RedirectUri: defaultRedirect)
            : new StorageConnectionResponse(
                connection.Provider.ToString(),
                connection.Status.ToString(),
                connection.AccountEmail,
                connection.RootFolderId,
                connection.ConnectedAt,
                connection.LastValidatedAt,
                connection.LastError,
                canConnect,
                connection.StoreProgressPhotos,
                connection.ProgressFolderId,
                connection.ProgressFolderName,
                connection.ClientId,
                !string.IsNullOrWhiteSpace(connection.ProtectedClientSecret),
                connection.RedirectUri ?? defaultRedirect);

    public static StoredFileResponse ToResponse(StoredFile file) =>
        new(
            file.Id,
            file.Provider.ToString(),
            file.FileName,
            file.ContentType,
            file.FileSize,
            file.LogicalKey,
            file.EntityType,
            file.EntityId,
            file.Status.ToString(),
            file.CreatedAt);
}
