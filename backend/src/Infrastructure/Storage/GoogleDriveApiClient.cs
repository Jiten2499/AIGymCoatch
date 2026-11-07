using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LifeFitAI.Application.Storage;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Infrastructure.Storage;

public sealed class GoogleDriveApiClient : IGoogleDriveApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleDriveApiClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public async Task<Result<(string Email, string? DisplayName)>> GetAccountAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = Authenticated(HttpMethod.Get, "https://www.googleapis.com/drive/v3/about?fields=user(displayName,emailAddress)", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<(string, string?)>(MapDriveError(response.StatusCode));
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var user = document.RootElement.GetProperty("user");
        var email = user.GetProperty("emailAddress").GetString();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<(string, string?)>(StorageErrors.ProviderUnavailable);
        }

        return Result.Success((email, user.TryGetProperty("displayName", out var name) ? name.GetString() : null));
    }

    public async Task<Result<GoogleDriveFolderIds>> EnsureLifeFitFoldersAsync(string accessToken, CancellationToken cancellationToken)
    {
        var root = await EnsureFolderAsync(accessToken, "LifeFit", null, cancellationToken).ConfigureAwait(false);
        if (root.IsFailure)
        {
            return Result.Failure<GoogleDriveFolderIds>(root.Error);
        }

        var members = await EnsureFolderAsync(accessToken, "Members", root.Value, cancellationToken).ConfigureAwait(false);
        if (members.IsFailure)
        {
            return Result.Failure<GoogleDriveFolderIds>(members.Error);
        }

        var communications = await EnsureFolderAsync(accessToken, "Communications", root.Value, cancellationToken).ConfigureAwait(false);
        if (communications.IsFailure)
        {
            return Result.Failure<GoogleDriveFolderIds>(communications.Error);
        }

        var gym = await EnsureFolderAsync(accessToken, "Gym", root.Value, cancellationToken).ConfigureAwait(false);
        if (gym.IsFailure)
        {
            return Result.Failure<GoogleDriveFolderIds>(gym.Error);
        }

        var other = await EnsureFolderAsync(accessToken, "Other", root.Value, cancellationToken).ConfigureAwait(false);
        if (other.IsFailure)
        {
            return Result.Failure<GoogleDriveFolderIds>(other.Error);
        }

        return Result.Success(new GoogleDriveFolderIds(root.Value, members.Value, communications.Value, gym.Value, other.Value));
    }

    public async Task<Result> ValidateFolderAsync(string accessToken, string folderId, CancellationToken cancellationToken)
    {
        using var request = Authenticated(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(folderId)}?fields=id,mimeType,trashed", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure(MapDriveError(response.StatusCode));
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (document.RootElement.TryGetProperty("trashed", out var trashed) && trashed.GetBoolean())
        {
            return Result.Failure(StorageErrors.NotConnected);
        }

        return Result.Success();
    }

    public async Task<Result<GoogleDriveFolder>> DescribeFolderAsync(string accessToken, string folderId, CancellationToken cancellationToken)
    {
        using var request = Authenticated(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(folderId)}?fields=id,name,mimeType,trashed", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<GoogleDriveFolder>(MapDriveError(response.StatusCode));
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return ReadFolder(document.RootElement);
    }

    public async Task<Result<IReadOnlyList<GoogleDriveFolder>>> ListChildFoldersAsync(string accessToken, string parentId, CancellationToken cancellationToken)
    {
        var query = $"'{parentId}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        using var request = Authenticated(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name,mimeType,trashed)&pageSize=50&orderBy=name", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<IReadOnlyList<GoogleDriveFolder>>(MapDriveError(response.StatusCode));
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var folders = new List<GoogleDriveFolder>();
        if (document.RootElement.TryGetProperty("files", out var files))
        {
            foreach (var file in files.EnumerateArray())
            {
                var folder = ReadFolder(file);
                if (folder.IsSuccess)
                {
                    folders.Add(folder.Value);
                }
            }
        }

        return Result.Success<IReadOnlyList<GoogleDriveFolder>>(folders);
    }

    private static Result<GoogleDriveFolder> ReadFolder(JsonElement element)
    {
        var id = element.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
        var name = element.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
        var mime = element.TryGetProperty("mimeType", out var mimeNode) ? mimeNode.GetString() : null;
        var trashed = element.TryGetProperty("trashed", out var trashedNode) && trashedNode.ValueKind == JsonValueKind.True;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || trashed || mime != "application/vnd.google-apps.folder")
        {
            return Result.Failure<GoogleDriveFolder>(StorageErrors.InvalidState);
        }

        return Result.Success(new GoogleDriveFolder(id, name));
    }

    public async Task<Result<GoogleDriveUploadedFile>> UploadAsync(
        string accessToken,
        string parentFolderId,
        string fileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["name"] = fileName,
            ["parents"] = new[] { parentFolderId },
        });
        var boundary = "lifefit_" + Guid.NewGuid().ToString("N");
        var preamble = Encoding.UTF8.GetBytes(
            $"--{boundary}\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n{metadata}\r\n" +
            $"--{boundary}\r\nContent-Type: {contentType}\r\n\r\n");
        var epilogue = Encoding.UTF8.GetBytes($"\r\n--{boundary}--");
        var body = new byte[preamble.Length + content.Length + epilogue.Length];
        Buffer.BlockCopy(preamble, 0, body, 0, preamble.Length);
        content.CopyTo(body.AsMemory(preamble.Length));
        Buffer.BlockCopy(epilogue, 0, body, preamble.Length + content.Length, epilogue.Length);

        using var request = Authenticated(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,size,parents", accessToken);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/related; boundary={boundary}");
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<GoogleDriveUploadedFile>(MapDriveError(response.StatusCode, StorageErrors.UploadFailed));
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var id = document.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result.Failure<GoogleDriveUploadedFile>(StorageErrors.UploadFailed);
        }

        var size = document.RootElement.TryGetProperty("size", out var sizeNode) && long.TryParse(sizeNode.GetString(), out var parsed)
            ? parsed
            : content.Length;
        return Result.Success(new GoogleDriveUploadedFile(id, parentFolderId, fileName, size));
    }

    public async Task<Result<byte[]>> DownloadAsync(string accessToken, string externalFileId, CancellationToken cancellationToken)
    {
        using var request = Authenticated(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(externalFileId)}?alt=media", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<byte[]>(MapDriveError(response.StatusCode, StorageErrors.DownloadFailed));
        }

        return Result.Success(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> DeleteAsync(string accessToken, string externalFileId, CancellationToken cancellationToken)
    {
        using var request = Authenticated(HttpMethod.Delete, $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(externalFileId)}", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound
            ? Result.Success()
            : Result.Failure(MapDriveError(response.StatusCode, StorageErrors.DeleteFailed));
    }

    public async Task<Result<(string FileName, string ContentType, long FileSize)>> GetMetadataAsync(
        string accessToken,
        string externalFileId,
        CancellationToken cancellationToken)
    {
        using var request = Authenticated(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(externalFileId)}?fields=name,mimeType,size", accessToken);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<(string, string, long)>(MapDriveError(response.StatusCode));
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var name = document.RootElement.GetProperty("name").GetString() ?? "file";
        var mime = document.RootElement.TryGetProperty("mimeType", out var mimeNode) ? mimeNode.GetString() ?? "application/octet-stream" : "application/octet-stream";
        var size = document.RootElement.TryGetProperty("size", out var sizeNode) && long.TryParse(sizeNode.GetString(), out var parsed) ? parsed : 0;
        return Result.Success((name, mime, size));
    }

    private async Task<Result<string>> EnsureFolderAsync(string accessToken, string name, string? parentId, CancellationToken cancellationToken)
    {
        var query = $"name = '{name.Replace("'", "\\'", StringComparison.Ordinal)}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            query += $" and '{parentId}' in parents";
        }

        using var listRequest = Authenticated(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name)&pageSize=1", accessToken);
        using var listResponse = await SendAsync(listRequest, cancellationToken).ConfigureAwait(false);
        if (!listResponse.IsSuccessStatusCode)
        {
            return Result.Failure<string>(MapDriveError(listResponse.StatusCode));
        }

        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (listDocument.RootElement.TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
        {
            var existing = files[0].GetProperty("id").GetString();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return Result.Success(existing);
            }
        }

        var metadata = new Dictionary<string, object>
        {
            ["name"] = name,
            ["mimeType"] = "application/vnd.google-apps.folder",
        };
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            metadata["parents"] = new[] { parentId };
        }

        using var createRequest = Authenticated(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files?fields=id", accessToken);
        createRequest.Content = new StringContent(JsonSerializer.Serialize(metadata), Encoding.UTF8, "application/json");
        using var createResponse = await SendAsync(createRequest, cancellationToken).ConfigureAwait(false);
        if (!createResponse.IsSuccessStatusCode)
        {
            return Result.Failure<string>(MapDriveError(createResponse.StatusCode));
        }

        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var id = created.RootElement.GetProperty("id").GetString();
        return string.IsNullOrWhiteSpace(id)
            ? Result.Failure<string>(StorageErrors.ProviderUnavailable)
            : Result.Success(id);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("google-drive");
        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage Authenticated(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static Error MapDriveError(HttpStatusCode status, Error? fallback = null) =>
        status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => StorageErrors.OAuthRevoked,
            HttpStatusCode.TooManyRequests => StorageErrors.ProviderUnavailable,
            HttpStatusCode.NotFound => StorageErrors.NotFound,
            _ => fallback ?? StorageErrors.ProviderUnavailable,
        };
}
