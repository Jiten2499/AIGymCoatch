using System.Text.Json;
using LifeFitAI.Application.Storage;
using LifeFitAI.Domain.Storage;
using LifeFitAI.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Infrastructure.Storage;

public sealed class GoogleDriveOAuthService : IGoogleDriveOAuthService
{
    private readonly GoogleDriveOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITenantStorageConnectionRepository _connections;
    private readonly IStorageSecretProtector _secrets;
    private GoogleOAuthApp? _resolved;

    public GoogleDriveOAuthService(
        IOptions<GoogleDriveOptions> options,
        IHttpClientFactory httpClientFactory,
        ITenantStorageConnectionRepository connections,
        IStorageSecretProtector secrets)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _connections = connections;
        _secrets = secrets;
    }

    public bool IsConfigured => _options.IsConfigured;

    public string RedirectUri => _options.RedirectUri;

    public string WebSuccessRedirectUri => _options.WebSuccessRedirectUri;

    public string MobileSuccessRedirectUri => _options.MobileSuccessRedirectUri;

    public async Task<bool> HasAppAsync(CancellationToken cancellationToken)
    {
        _resolved = await ResolveAsync(cancellationToken).ConfigureAwait(false);
        return _resolved is not null;
    }

    public string BuildAuthorizationUrl(string state, string codeChallenge)
    {
        var app = _resolved ?? OptionsApp();
        if (app is null)
        {
            throw new InvalidOperationException("Google Drive OAuth is not configured.");
        }

        var query = new Dictionary<string, string>
        {
            ["client_id"] = app.ClientId,
            ["redirect_uri"] = app.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = _options.Scope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        };
        return "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join('&', query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
    }

    public async Task<Result<GoogleTokenSet>> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken)
    {
        var app = await ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (app is null)
        {
            return Result.Failure<GoogleTokenSet>(StorageErrors.ConfigurationMissing);
        }

        return await TokenAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = app.ClientId,
            ["client_secret"] = app.ClientSecret,
            ["redirect_uri"] = app.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier,
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<GoogleTokenSet>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var app = await ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (app is null)
        {
            return Result.Failure<GoogleTokenSet>(StorageErrors.ConfigurationMissing);
        }

        return await TokenAsync(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = app.ClientId,
            ["client_secret"] = app.ClientSecret,
            ["grant_type"] = "refresh_token",
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GoogleOAuthApp?> ResolveAsync(CancellationToken cancellationToken)
    {
        var connection = await _connections.GetPlatformAsync(StorageProviderKind.GoogleDrive, cancellationToken).ConfigureAwait(false);
        var clientId = connection?.ClientId;
        var redirectUri = connection?.RedirectUri;
        if (!string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(redirectUri)
            && connection?.ProtectedClientSecret is { Length: > 0 } protectedSecret)
        {
            var secret = _secrets.Unprotect(protectedSecret);
            if (!string.IsNullOrWhiteSpace(secret))
            {
                return new GoogleOAuthApp(clientId, secret, redirectUri);
            }
        }

        return OptionsApp();
    }

    private GoogleOAuthApp? OptionsApp() =>
        _options.IsConfigured ? new GoogleOAuthApp(_options.ClientId, _options.ClientSecret, _options.RedirectUri) : null;

    private async Task<Result<GoogleTokenSet>> TokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("google-oauth");
        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<GoogleTokenSet>(MapTokenError(body));
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Result.Failure<GoogleTokenSet>(StorageErrors.OAuthInvalid);
        }

        return Result.Success(new GoogleTokenSet(
            accessToken,
            root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null,
            root.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
            root.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 3600));
    }

    private static Error MapTokenError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var error = document.RootElement.TryGetProperty("error", out var code) ? code.GetString() : null;
            if (string.Equals(error, "invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                return StorageErrors.OAuthRevoked;
            }
        }
        catch (JsonException)
        {
        }

        return StorageErrors.OAuthInvalid;
    }

    private sealed record GoogleOAuthApp(string ClientId, string ClientSecret, string RedirectUri);
}
