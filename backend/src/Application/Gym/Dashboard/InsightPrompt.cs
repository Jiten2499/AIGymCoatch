using System.Text.Json;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record AiInsightItem(string Type, string Title, string Message, string Priority);

public sealed record AiRecommendation(string Title, string Description);

public sealed record AiInsightSection(string Status, string? Summary, IReadOnlyList<AiInsightItem> Insights, IReadOnlyList<AiRecommendation> Recommendations)
{
    public static AiInsightSection Disabled { get; } = new("Disabled", null, [], []);

    public static AiInsightSection Empty { get; } = new("Empty", null, [], []);

    public static AiInsightSection Unavailable { get; } = new("Unavailable", null, [], []);
}

public sealed record BusinessDashboardInsights(IReadOnlyList<AttentionItem> Attention, AiInsightSection Ai);

public static class InsightPrompts
{
    public const string Version = "business-insights-v1";

    public const string System =
        "You are a business analytics assistant for a fitness/gym management platform (" + Version + "). " +
        "Use only the supplied facts. Do not invent metrics, events, revenue, member counts, percentages, or business events. " +
        "If a metric is missing, say that data is unavailable. Distinguish observations from recommendations. " +
        "Do not provide medical advice. Do not mention personal information or identifiers. " +
        "Return JSON only: {\"summary\":\"string\",\"insights\":[{\"type\":\"Positive|Warning|Attention|Neutral\",\"title\":\"string\",\"message\":\"string\",\"priority\":\"Low|Medium|High\"}],\"recommendations\":[{\"title\":\"string\",\"description\":\"string\"}]}. " +
        "At most 4 insights and 3 recommendations.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string User(BusinessInsightFacts facts) => "Facts: " + JsonSerializer.Serialize(facts, Json);
}

public static class InsightResponseReader
{
    public const int MaxInsights = 4;
    public const int MaxRecommendations = 3;

    private static readonly HashSet<string> Types = new(StringComparer.Ordinal) { "Positive", "Warning", "Attention", "Neutral" };
    private static readonly HashSet<string> Priorities = new(StringComparer.Ordinal) { "Low", "Medium", "High" };

    public static AiInsightSection? Read(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(Unwrap(content));
            var root = document.RootElement;
            var summary = Clip(Text(root, "summary"), 400);
            if (summary.Length == 0)
            {
                return null;
            }

            var insights = ReadInsights(root);
            var recommendations = ReadRecommendations(root);
            return new AiInsightSection("Ready", summary, insights, recommendations);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<AiInsightItem> ReadInsights(JsonElement root)
    {
        if (!root.TryGetProperty("insights", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var insights = new List<AiInsightItem>();
        foreach (var item in items.EnumerateArray())
        {
            if (insights.Count == MaxInsights)
            {
                break;
            }

            var type = Text(item, "type");
            var priority = Text(item, "priority");
            var title = Clip(Text(item, "title"), 80);
            var message = Clip(Text(item, "message"), 280);
            if (Types.Contains(type) && Priorities.Contains(priority) && title.Length > 0 && message.Length > 0)
            {
                insights.Add(new AiInsightItem(type, title, message, priority));
            }
        }

        return insights;
    }

    private static IReadOnlyList<AiRecommendation> ReadRecommendations(JsonElement root)
    {
        if (!root.TryGetProperty("recommendations", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var recommendations = new List<AiRecommendation>();
        foreach (var item in items.EnumerateArray())
        {
            if (recommendations.Count == MaxRecommendations)
            {
                break;
            }

            var title = Clip(Text(item, "title"), 80);
            var description = Clip(Text(item, "description"), 280);
            if (title.Length > 0 && description.Length > 0)
            {
                recommendations.Add(new AiRecommendation(title, description));
            }
        }

        return recommendations;
    }

    private static string Unwrap(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('\n');
        var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return start >= 0 && end > start ? trimmed[(start + 1)..end].Trim() : trimmed;
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string Clip(string value, int max) => value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}
