using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using LifeFitAI.Application.Ai;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Infrastructure.Ai;

public sealed class DatabaseLlmRuntime : ILlmRuntime
{
    private readonly ILlmConnectionStore _store;
    private readonly ILlmSecretProtector _secrets;
    private readonly AiOptions _options;

    public DatabaseLlmRuntime(ILlmConnectionStore store, ILlmSecretProtector secrets, IOptions<AiOptions> options)
    {
        _store = store;
        _secrets = secrets;
        _options = options.Value;
    }

    public async Task<LlmCallSettings?> ResolveAsync(CancellationToken cancellationToken)
    {
        var connection = await _store.GetDefaultActiveAsync(cancellationToken).ConfigureAwait(false);
        if (connection is not null)
        {
            var key = _secrets.Unprotect(connection.ProtectedApiKey);
            if (!string.IsNullOrWhiteSpace(key))
            {
                return new LlmCallSettings(connection.Provider, connection.BaseUrl, key, connection.DefaultModel, connection.TimeoutSeconds);
            }
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return null;
        }

        return new LlmCallSettings(_options.Provider, _options.BaseUrl.TrimEnd('/'), _options.ApiKey, _options.Model, Math.Clamp(_options.TimeoutSeconds, 1, 60));
    }
}

public sealed class OpenAiCompatibleLlmProbe : ILlmProbe
{
    private readonly IHttpClientFactory _http;

    public OpenAiCompatibleLlmProbe(IHttpClientFactory http) => _http = http;

    public async Task<LlmProbeResult> ProbeAsync(string baseUrl, string apiKey, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 60)));
        using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl.TrimEnd('/') + "/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var watch = Stopwatch.StartNew();
        try
        {
            var client = _http.CreateClient("llm");
            using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            watch.Stop();
            if (!response.IsSuccessStatusCode)
            {
                return new LlmProbeResult(false, (int)watch.ElapsedMilliseconds, []);
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false), cancellationToken: timeout.Token).ConfigureAwait(false);
            var models = new List<string>();
            if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } value)
                    {
                        models.Add(value);
                    }
                }
            }

            return new LlmProbeResult(true, (int)watch.ElapsedMilliseconds, models);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            watch.Stop();
            return new LlmProbeResult(false, (int)watch.ElapsedMilliseconds, []);
        }
    }
}
