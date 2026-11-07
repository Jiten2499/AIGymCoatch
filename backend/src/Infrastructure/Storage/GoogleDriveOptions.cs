namespace LifeFitAI.Infrastructure.Storage;

public sealed class GoogleDriveOptions
{
    public const string SectionName = "GoogleDrive";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string WebSuccessRedirectUri { get; set; } = "/business/integrations";

    public string MobileSuccessRedirectUri { get; set; } = "lifefit://integrations/google-drive";

    public string Scope { get; set; } = "https://www.googleapis.com/auth/drive.file";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);
}
