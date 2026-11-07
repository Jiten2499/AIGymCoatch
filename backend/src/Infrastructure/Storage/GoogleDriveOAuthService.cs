using System.Text.Json;
using LifeFitAI.Application.Storage;
using LifeFitAI.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Infrastructure.Storage;

public sealed class GoogleDriveOAuthService : IGoogleDriveOAuthService
{
    private readonly GoogleDriveOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleDriveOAuthService(IOptions<GoogleDriveOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public bool IsConfigured => _options.IsConfigured;

    public string WebSuccessRedirectUri => _options.WebSuccessRedirectUri;

    public string MobileSuccessRedirectUri => _options.MobileSuccessRedirectUri;

    public string BuildAuthorizationUrl(string state, string codeChallenge)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
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

    public Task<Result<GoogleTokenSet>> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken) =>
        TokenAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier,
        }, cancellationToken);

    public Task<Result<GoogleTokenSet>> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        TokenAsync(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "refresh_token",
        }, cancellationToken);

    private async Task<Result<GoogleTokenSet>> TokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return Result.Failure<GoogleTokenSet>(StorageErrors.ConfigurationMissing);
        }

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
}
