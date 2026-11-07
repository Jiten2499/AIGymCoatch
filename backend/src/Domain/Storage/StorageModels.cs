using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Storage;

public enum StorageProviderKind
{
    GoogleDrive = 1,
}

public enum StorageConnectionStatus
{
    NotConnected = 1,
    Connecting = 2,
    Connected = 3,
    Failed = 4,
    Revoked = 5,
}

public enum StoredFileStatus
{
    Active = 1,
    Deleted = 2,
}

public enum StorageOAuthClientKind
{
    Web = 1,
    Mobile = 2,
}

public sealed class TenantStorageConnection : BaseEntity
{
    private TenantStorageConnection()
    {
    }

    public Guid TenantId { get; private set; }

    public StorageProviderKind Provider { get; private set; }

    public StorageConnectionStatus Status { get; private set; }

    public string? AccountEmail { get; private set; }

    public string? RootFolderId { get; private set; }

    public string? MembersFolderId { get; private set; }

    public string? CommunicationsFolderId { get; private set; }

    public string? GymFolderId { get; private set; }

    public string? OtherFolderId { get; private set; }

    public string? ProtectedRefreshToken { get; private set; }

    public string? GrantedScopes { get; private set; }

    public Guid? ConnectedByUserId { get; private set; }

    public DateTime? ConnectedAt { get; private set; }

    public DateTime? LastValidatedAt { get; private set; }

    public string? LastError { get; private set; }

    public bool StoreProgressPhotos { get; private set; } = true;

    public string? ProgressFolderId { get; private set; }

    public string? ProgressFolderName { get; private set; }

    public static TenantStorageConnection Create(Guid id, Guid tenantId, StorageProviderKind provider) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Provider = provider,
            Status = StorageConnectionStatus.NotConnected,
        };

    public void MarkConnecting()
    {
        Status = StorageConnectionStatus.Connecting;
        LastError = null;
    }

    public void Connect(
        string accountEmail,
        string protectedRefreshToken,
        string grantedScopes,
        string rootFolderId,
        string membersFolderId,
        string communicationsFolderId,
        string gymFolderId,
        string otherFolderId,
        Guid connectedByUserId,
        DateTime utcNow)
    {
        AccountEmail = accountEmail.Trim();
        ProtectedRefreshToken = protectedRefreshToken;
        GrantedScopes = grantedScopes;
        RootFolderId = rootFolderId;
        MembersFolderId = membersFolderId;
        CommunicationsFolderId = communicationsFolderId;
        GymFolderId = gymFolderId;
        OtherFolderId = otherFolderId;
        ConnectedByUserId = connectedByUserId;
        ConnectedAt = utcNow;
        LastValidatedAt = utcNow;
        Status = StorageConnectionStatus.Connected;
        LastError = null;
    }

    public void MarkValidated(DateTime utcNow)
    {
        LastValidatedAt = utcNow;
        if (Status is StorageConnectionStatus.Failed or StorageConnectionStatus.Revoked)
        {
            Status = StorageConnectionStatus.Connected;
        }

        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Status = StorageConnectionStatus.Failed;
        LastError = Truncate(error);
    }

    public void MarkRevoked(string? error = null)
    {
        Status = StorageConnectionStatus.Revoked;
        ProtectedRefreshToken = null;
        LastError = Truncate(error ?? "Google authorization was revoked.");
    }

    public void Disconnect()
    {
        Status = StorageConnectionStatus.NotConnected;
        ProtectedRefreshToken = null;
        AccountEmail = null;
        RootFolderId = null;
        MembersFolderId = null;
        CommunicationsFolderId = null;
        GymFolderId = null;
        OtherFolderId = null;
        GrantedScopes = null;
        ConnectedByUserId = null;
        ConnectedAt = null;
        LastValidatedAt = null;
        LastError = null;
        ProgressFolderId = null;
        ProgressFolderName = null;
    }

    public void SetProgressPhotoStorage(bool enabled) => StoreProgressPhotos = enabled;

    public bool SetProgressFolder(string folderId, string folderName)
    {
        var id = folderId.Trim();
        var name = folderName.Trim();
        if (id.Length is 0 or > 128 || name.Length is 0 or > 200)
        {
            return false;
        }

        ProgressFolderId = id;
        ProgressFolderName = name;
        return true;
    }

    public string? FolderForCategory(string category) => category.ToLowerInvariant() switch
    {
        "progress" => ProgressFolderId ?? MembersFolderId,
        "members" => MembersFolderId,
        "communications" => CommunicationsFolderId,
        "gym" => GymFolderId,
        _ => OtherFolderId,
    };

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}

public sealed class StoredFile : BaseEntity
{
    private StoredFile()
    {
    }

    public Guid TenantId { get; private set; }

    public StorageProviderKind Provider { get; private set; }

    public string ExternalFileId { get; private set; } = string.Empty;

    public string ParentFolderId { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long FileSize { get; private set; }

    public string LogicalKey { get; private set; } = string.Empty;

    public string? EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public StoredFileStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public static StoredFile Create(
        Guid id,
        Guid tenantId,
        StorageProviderKind provider,
        string externalFileId,
        string parentFolderId,
        string fileName,
        string contentType,
        long fileSize,
        string logicalKey,
        string? entityType,
        Guid? entityId,
        Guid createdByUserId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Provider = provider,
            ExternalFileId = externalFileId,
            ParentFolderId = parentFolderId,
            FileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            LogicalKey = logicalKey,
            EntityType = entityType,
            EntityId = entityId,
            Status = StoredFileStatus.Active,
            CreatedByUserId = createdByUserId,
        };

    public void MarkDeleted() => Status = StoredFileStatus.Deleted;
}

public sealed class StorageOAuthState
{
    private StorageOAuthState()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string StateHash { get; private set; } = string.Empty;

    public string CodeVerifier { get; private set; } = string.Empty;

    public StorageOAuthClientKind ClientKind { get; private set; }

    public string? ReturnPath { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? ConsumedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static StorageOAuthState Create(
        Guid id,
        Guid tenantId,
        Guid userId,
        string stateHash,
        string codeVerifier,
        StorageOAuthClientKind clientKind,
        string? returnPath,
        DateTime createdAt,
        DateTime expiresAt) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            StateHash = stateHash,
            CodeVerifier = codeVerifier,
            ClientKind = clientKind,
            ReturnPath = returnPath,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };

    public bool TryConsume(DateTime utcNow)
    {
        if (ConsumedAt is not null || ExpiresAt <= utcNow)
        {
            return false;
        }

        ConsumedAt = utcNow;
        return true;
    }
}
