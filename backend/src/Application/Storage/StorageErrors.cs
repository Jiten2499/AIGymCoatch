using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Storage;

public static class StorageErrors
{
    public static readonly Error FeatureDisabled = new("storage.feature_disabled", "Google Drive storage is not enabled for this workspace.");
    public static readonly Error NotFound = new("storage.not_found", "The storage connection or file was not found in this workspace.");
    public static readonly Error NotConnected = new("storage.not_connected", "Google Drive is not connected for this workspace.");
    public static readonly Error InvalidState = new("storage.invalid_state", "The storage connection cannot change to that state.");
    public static readonly Error OAuthDenied = new("storage.oauth_denied", "Google authorization was denied.");
    public static readonly Error OAuthInvalid = new("storage.oauth_invalid", "The Google authorization response was invalid or expired.");
    public static readonly Error OAuthRevoked = new("storage.oauth_revoked", "Google Drive authorization was revoked. Reconnect the account.");
    public static readonly Error ProviderUnavailable = new("storage.provider_unavailable", "Google Drive is temporarily unavailable.");
    public static readonly Error UploadFailed = new("storage.upload_failed", "The file could not be uploaded.");
    public static readonly Error DownloadFailed = new("storage.download_failed", "The file could not be downloaded.");
    public static readonly Error DeleteFailed = new("storage.delete_failed", "The file could not be deleted.");
    public static readonly Error InvalidFile = new("storage.invalid_file", "The file type or size is not allowed.");
    public static readonly Error InvalidKey = new("storage.invalid_key", "The storage key is not valid for this workspace.");
    public static readonly Error Conflict = new("storage.conflict", "A file with that key already exists.");
    public static readonly Error ConfigurationMissing = new("storage.configuration_missing", "Google Drive OAuth is not configured on the server.");
}
