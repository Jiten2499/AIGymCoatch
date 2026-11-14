using System.Text.Json;

namespace LifeFitAI.Application.Fitness.Ai;

internal static class PlanJson
{
    public static string? Extract(string json)
    {
        var text = json.Trim();
        const string finalChannel = "<|channel|>final<|message|>";
        var channel = text.LastIndexOf(finalChannel, StringComparison.Ordinal);
        if (channel >= 0)
        {
            text = text[(channel + finalChannel.Length)..].Trim();
        }

        if (!text.StartsWith('{'))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            text = text[start..(end + 1)];
        }

        return text;
    }

    public static bool Array(JsonElement element, out List<JsonElement> items, params string[] names)
    {
        items = [];
        var value = Property(element, names);
        if (value is null || value.Value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        items = value.Value.EnumerateArray().ToList();
        return true;
    }

    public static string? Text(JsonElement element, params string[] names)
    {
        var value = Property(element, names);
        if (value is null)
        {
            return null;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.GetRawText(),
            _ => null,
        };
    }

    public static int? Integer(JsonElement element, params string[] names)
    {
        var value = Property(element, names);
        if (value is null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.Value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.Value.GetString()?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (int.TryParse(text, out var parsed))
        {
            return parsed;
        }

        var length = 0;
        while (length < text.Length && char.IsDigit(text[length]))
        {
            length++;
        }

        return length > 0 && int.TryParse(text[..length], out var leading) ? leading : null;
    }

    public static decimal? Decimal(JsonElement element, params string[] names)
    {
        var value = Property(element, names);
        if (value is null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDecimal(out var number))
        {
            return number;
        }

        return value.Value.ValueKind == JsonValueKind.String && decimal.TryParse(value.Value.GetString(), out var parsed) ? parsed : null;
    }

    public static bool Flag(JsonElement element, params string[] names)
    {
        var value = Property(element, names);
        if (value is null)
        {
            return false;
        }

        if (value.Value.ValueKind is JsonValueKind.True)
        {
            return true;
        }

        return value.Value.ValueKind == JsonValueKind.String && bool.TryParse(value.Value.GetString(), out var flag) && flag;
    }

    public static bool Match(string? candidate, IReadOnlySet<Guid> allowedIds, IReadOnlyDictionary<string, Guid>? exerciseNames, out Guid exerciseId)
    {
        exerciseId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(candidate) || exerciseNames is null)
        {
            return false;
        }

        var trimmed = candidate.Trim();
        if (exerciseNames.TryGetValue(trimmed, out exerciseId) && allowedIds.Contains(exerciseId))
        {
            return true;
        }

        var normalized = Normalize(trimmed);
        Guid? match = null;
        var distance = int.MaxValue;
        foreach (var pair in exerciseNames)
        {
            if (!allowedIds.Contains(pair.Value))
            {
                continue;
            }

            var key = Normalize(pair.Key);
            if (key.Length == 0 || (!key.Contains(normalized, StringComparison.Ordinal) && !normalized.Contains(key, StringComparison.Ordinal)))
            {
                continue;
            }

            var gap = Math.Abs(key.Length - normalized.Length);
            if (gap < distance)
            {
                match = pair.Value;
                distance = gap;
            }
            else if (gap == distance && match != pair.Value)
            {
                match = null;
            }
        }

        if (match is null)
        {
            return false;
        }

        exerciseId = match.Value;
        return true;
    }

    private static string Normalize(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
        return new string(chars);
    }

    private static JsonElement? Property(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                return property.Value;
            }
        }

        return null;
    }
}
