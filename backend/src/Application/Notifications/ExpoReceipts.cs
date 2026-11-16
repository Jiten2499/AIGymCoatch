using System.Text.Json;

namespace LifeFitAI.Application.Notifications;

public sealed record ExpoSendTicket(bool Accepted, string? TicketId, string? Error);

public static class ExpoReceipts
{
    public static IReadOnlyList<ExpoSendTicket> ReadSend(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray().Select(ReadTicket).ToArray();
    }

    public static IReadOnlyDictionary<string, ExpoSendTicket> ReadReceipts(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, ExpoSendTicket>();
        }

        var receipts = new Dictionary<string, ExpoSendTicket>(StringComparer.Ordinal);
        foreach (var property in data.EnumerateObject())
        {
            receipts[property.Name] = ReadTicket(property.Value);
        }

        return receipts;
    }

    public static bool DeviceGone(string? error) => error == "DeviceNotRegistered";

    private static ExpoSendTicket ReadTicket(JsonElement ticket)
    {
        var status = ticket.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
        var id = ticket.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
        string? error = null;
        if (ticket.TryGetProperty("details", out var details) && details.TryGetProperty("error", out var detailError))
        {
            error = detailError.GetString();
        }
        else if (ticket.TryGetProperty("message", out var message))
        {
            error = message.GetString();
        }

        return new ExpoSendTicket(status == "ok", id, error);
    }
}
