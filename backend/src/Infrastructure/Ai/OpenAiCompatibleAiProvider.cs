using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LifeFitAI.Application.Ai;
using LifeFitAI.Application.Fitness;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "Groq";

    public string Model { get; set; } = "openai/gpt-oss-120b";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 20;

    public int MaxTokens { get; set; } = 800;

    public int MaxPromptCharacters { get; set; } = 4000;

    public int DailyRequestLimit { get; set; } = 20;

    public int MonthlyRequestLimit { get; set; } = 200;

    public int TenantDailyRequestLimit { get; set; } = 200;

    public int TenantMonthlyRequestLimit { get; set; } = 2000;

    public decimal CostPerThousandTokens { get; set; }
}

public sealed class AiLimits : IAiLimits
{
    public AiLimits(IOptions<AiOptions> options)
    {
        var value = options.Value;
        Provider = string.IsNullOrWhiteSpace(value.Provider) ? "Unconfigured" : value.Provider;
        Model = string.IsNullOrWhiteSpace(value.Model) ? "unconfigured" : value.Model;
        MaxTokens = Math.Clamp(value.MaxTokens, 1, 4000);
        MaxPromptCharacters = Math.Clamp(value.MaxPromptCharacters, 200, 8000);
        DailyRequestLimit = Math.Max(0, value.DailyRequestLimit);
        MonthlyRequestLimit = Math.Max(0, value.MonthlyRequestLimit);
        TenantDailyRequestLimit = Math.Max(0, value.TenantDailyRequestLimit);
        TenantMonthlyRequestLimit = Math.Max(0, value.TenantMonthlyRequestLimit);
        CostPerThousandTokens = value.CostPerThousandTokens < 0 ? 0 : value.CostPerThousandTokens;
    }

    public string Provider { get; }

    public string Model { get; }

    public int MaxTokens { get; }

    public int MaxPromptCharacters { get; }

    public int DailyRequestLimit { get; }

    public int MonthlyRequestLimit { get; }

    public int TenantDailyRequestLimit { get; }

    public int TenantMonthlyRequestLimit { get; }

    public decimal CostPerThousandTokens { get; }
}

public sealed class OpenAiCompatibleAiProvider : IAiProvider
{
    private readonly IHttpClientFactory _http;
    private readonly ILlmRuntime _runtime;
    private readonly ILogger<OpenAiCompatibleAiProvider> _logger;

    public OpenAiCompatibleAiProvider(IHttpClientFactory http, ILlmRuntime runtime, ILogger<OpenAiCompatibleAiProvider> logger)
    {
        _http = http;
        _runtime = runtime;
        _logger = logger;
    }

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken)
    {
        var settings = await _runtime.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return new AiCompletionResult(false, null, 0, 0, "unavailable");
        }

            var model = string.IsNullOrWhiteSpace(settings.Model) ? request.Model : settings.Model;
            var reasoningModel = model.Contains("gpt-oss", StringComparison.OrdinalIgnoreCase);
            var maxTokens = reasoningModel ? Math.Clamp(Math.Max(request.MaxTokens, 2000) + 1500, 1, 4000) : request.MaxTokens;
            var timeoutSeconds = reasoningModel ? Math.Max(settings.TimeoutSeconds, 45) : settings.TimeoutSeconds;
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl.TrimEnd('/') + "/chat/completions");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                var payload = new Dictionary<string, object?>
                {
                    ["model"] = model,
                    ["max_tokens"] = maxTokens,
                    ["messages"] = request.Messages.Select(item => new { role = item.Role, content = item.Content }).ToArray(),
                };
                if (reasoningModel)
                {
                    payload["reasoning_effort"] = "low";
                }

                message.Content = JsonContent.Create(payload);

                try
                {
                    var result = await SendAsync(message, timeoutSeconds, cancellationToken).ConfigureAwait(false);
                return result with { Provider = settings.Provider, Model = model };
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("AI provider connection failed");
                if (attempt == 2)
                {
                    return new AiCompletionResult(false, null, 0, 0, "unavailable", settings.Provider, model);
                }
            }
        }

        return new AiCompletionResult(false, null, 0, 0, "unavailable", settings.Provider, model);
    }

    private async Task<AiCompletionResult> SendAsync(HttpRequestMessage message, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 60)));
        try
        {
            var client = _http.CreateClient("llm");
            using var response = await client.SendAsync(message, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("AI provider returned a rate limit");
                return new AiCompletionResult(false, null, 0, 0, "quota");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI provider returned status {StatusCode}", (int)response.StatusCode);
                return new AiCompletionResult(false, null, 0, 0, "unavailable");
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false), cancellationToken: timeout.Token).ConfigureAwait(false);
            var choice = document.RootElement.GetProperty("choices")[0];
            var content = ReadContent(choice);
            var input = 0;
            var output = 0;
            if (document.RootElement.TryGetProperty("usage", out var usage))
            {
                input = usage.TryGetProperty("prompt_tokens", out var prompt) ? prompt.GetInt32() : 0;
                output = usage.TryGetProperty("completion_tokens", out var completion) ? completion.GetInt32() : 0;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                var finish = choice.TryGetProperty("finish_reason", out var reason) ? reason.GetString() : null;
                _logger.LogWarning("AI provider returned no content. Finish reason {FinishReason}", finish);
                return new AiCompletionResult(false, null, input, output, "invalid");
            }

            return new AiCompletionResult(true, content, input, output, string.Empty);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("AI provider timed out");
            return new AiCompletionResult(false, null, 0, 0, "timeout");
        }
        catch (JsonException)
        {
            _logger.LogWarning("AI provider request failed");
            return new AiCompletionResult(false, null, 0, 0, "unavailable");
        }
    }

    private static string? ReadContent(JsonElement choice)
    {
        if (!choice.TryGetProperty("message", out var message))
        {
            return null;
        }

        var content = UsableAnswer(ReadText(message, "content"));
        return content ?? UsableAnswer(ReadText(message, "reasoning") ?? ReadText(message, "reasoning_content"), requireFinal: true);
    }

    private static string? ReadText(JsonElement message, string name)
    {
        if (!message.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var part in value.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                parts.Add(part.GetString() ?? string.Empty);
            }
            else if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                parts.Add(text.GetString() ?? string.Empty);
            }
        }

        return string.Concat(parts);
    }

    private static string? UsableAnswer(string? text, bool requireFinal = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        const string marker = "<|channel|>final<|message|>";
        var channel = text.LastIndexOf(marker, StringComparison.Ordinal);
        if (channel >= 0)
        {
            var answer = text[(channel + marker.Length)..].Trim();
            return string.IsNullOrWhiteSpace(answer) ? null : answer;
        }

        if (requireFinal || text.Contains("<|channel|>", StringComparison.Ordinal))
        {
            return null;
        }

        return text.Trim();
    }
}
