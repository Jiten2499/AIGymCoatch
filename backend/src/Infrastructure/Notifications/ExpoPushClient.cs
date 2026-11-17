using System.Net.Http.Headers;
using System.Net.Http.Json;
using LifeFitAI.Application.Notifications;
using Microsoft.Extensions.Configuration;

namespace LifeFitAI.Infrastructure.Notifications;

public sealed record ExpoPushMessage(string Token, string Title, string Body, string? Sound, string ChannelId, string Priority, IReadOnlyDictionary<string, string> Data);

public interface IExpoPushClient
{
    Task<IReadOnlyList<ExpoSendTicket>> SendAsync(IReadOnlyList<ExpoPushMessage> messages, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, ExpoSendTicket>> ReceiptsAsync(IReadOnlyList<string> ticketIds, CancellationToken cancellationToken);
}

public sealed class ExpoPushClient : IExpoPushClient
{
    private readonly IHttpClientFactory _http;
    private readonly string? _accessToken;

    public ExpoPushClient(IHttpClientFactory http, IConfiguration configuration)
    {
        _http = http;
        var configured = configuration["Expo:AccessToken"];
        _accessToken = string.IsNullOrWhiteSpace(configured) ? configuration["EXPO_ACCESS_TOKEN"] : configured;
    }

    public async Task<IReadOnlyList<ExpoSendTicket>> SendAsync(IReadOnlyList<ExpoPushMessage> messages, CancellationToken cancellationToken)
    {
        var tickets = new List<ExpoSendTicket>(messages.Count);
        foreach (var batch in messages.Chunk(100))
        {
            var payload = batch.Select(message => new Dictionary<string, object?>
            {
                ["to"] = message.Token,
                ["title"] = message.Title,
                ["body"] = message.Body,
                ["sound"] = message.Sound,
                ["priority"] = message.Priority,
                ["channelId"] = message.ChannelId,
                ["color"] = "#06130d",
                ["data"] = message.Data,
            }).ToArray();
            using var response = await PostAsync("push/send", payload, cancellationToken).ConfigureAwait(false);
            tickets.AddRange(ExpoReceipts.ReadSend(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)));
        }

        return tickets;
    }

    public async Task<IReadOnlyDictionary<string, ExpoSendTicket>> ReceiptsAsync(IReadOnlyList<string> ticketIds, CancellationToken cancellationToken)
    {
        var receipts = new Dictionary<string, ExpoSendTicket>(StringComparer.Ordinal);
        foreach (var batch in ticketIds.Chunk(300))
        {
            using var response = await PostAsync("push/getReceipts", new { ids = batch }, cancellationToken).ConfigureAwait(false);
            foreach (var pair in ExpoReceipts.ReadReceipts(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)))
            {
                receipts[pair.Key] = pair.Value;
            }
        }

        return receipts;
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object payload, CancellationToken cancellationToken)
    {
        var client = _http.CreateClient("expo-push");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.Dispose();
        if (detail.Length > 240)
        {
            detail = detail[..240];
        }

        throw new HttpRequestException($"Expo push was rejected ({(int)response.StatusCode}). {detail}");
    }
}
