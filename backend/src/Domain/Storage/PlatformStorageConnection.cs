using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Storage;

public sealed class PlatformStorageConnection : BaseEntity
{
    private PlatformStorageConnection()
    {
    }

    public StorageProviderKind Provider { get; private set; }

    public StorageConnectionStatus Status { get; private set; }

    public string? ClientId { get; private set; }

    public string? ProtectedClientSecret { get; private set; }

    public string? RedirectUri { get; private set; }

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

    public static PlatformStorageConnection Create(Guid id, StorageProviderKind provider) =>
        new()
        {
            Id = id,
            Provider = provider,
            Status = StorageConnectionStatus.NotConnected,
        };

    public void SetApp(string clientId, string redirectUri, string? protectedClientSecret)
    {
        ClientId = clientId.Trim();
        RedirectUri = redirectUri.Trim();
        if (protectedClientSecret is not null)
        {
            ProtectedClientSecret = protectedClientSecret;
        }
    }

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
