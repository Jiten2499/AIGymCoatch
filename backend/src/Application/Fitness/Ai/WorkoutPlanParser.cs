using System.Text.Json;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Fitness.Ai;

public static class WorkoutPlanParser
{
    public static Result<GeneratedProgram> Parse(string? json, IReadOnlySet<Guid> allowedIds, IReadOnlyDictionary<string, Guid>? exerciseNames = null)
    {
        if (string.IsNullOrWhiteSpace(json) || allowedIds.Count == 0 || ExtractJson(json) is not string text)
        {
            return Result.Failure<GeneratedProgram>(AiErrors.InvalidOutput);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return Result.Failure<GeneratedProgram>(AiErrors.InvalidOutput);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryArray(root, out var dayElements, "days", "workouts", "schedule") || dayElements.Count is < 1 or > 14)
            {
                return Result.Failure<GeneratedProgram>(AiErrors.InvalidOutput);
            }

            var days = new List<GeneratedDay>();
            for (var index = 0; index < dayElements.Count; index++)
            {
                if (!TryDay(dayElements[index], index, allowedIds, exerciseNames, out var day))
                {
                    return Result.Failure<GeneratedProgram>(AiErrors.InvalidOutput);
                }

                days.Add(day);
            }

            if (days.Select(day => day.DayNumber).Distinct().Count() != days.Count)
            {
                return Result.Failure<GeneratedProgram>(AiErrors.InvalidOutput);
            }

            var name = Text(root, "name", "title", "programName") ?? "Workout plan";
            return Result.Success(new GeneratedProgram(name.Trim(), days));
        }
    }

    private static bool TryDay(JsonElement day, int index, IReadOnlySet<Guid> allowedIds, IReadOnlyDictionary<string, Guid>? exerciseNames, out GeneratedDay parsed)
    {
        parsed = new GeneratedDay(0, string.Empty, false, []);
        if (day.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var number = Integer(day, "dayNumber", "day", "number") ?? index + 1;
        if (number is < 1 or > 14)
        {
            return false;
        }

        var rest = Flag(day, "restDay", "rest");
        var exercises = new List<GeneratedExercise>();
        if (!rest && TryArray(day, out var items, "exercises", "items", "movements"))
        {
            foreach (var item in items)
            {
                if (item.ValueKind == JsonValueKind.Object && TryResolve(item, allowedIds, exerciseNames, out var exercise))
                {
                    exercises.Add(exercise);
                }

                if (exercises.Count == 12)
                {
                    break;
                }
            }
        }

        if (!rest && exercises.Count == 0)
        {
            return false;
        }

        parsed = new GeneratedDay(number, (Text(day, "name", "title") ?? $"Day {number}").Trim(), rest, exercises);
        return true;
    }

    private static bool TryResolve(JsonElement item, IReadOnlySet<Guid> allowedIds, IReadOnlyDictionary<string, Guid>? exerciseNames, out GeneratedExercise exercise)
    {
        exercise = new GeneratedExercise(Guid.Empty, 0, null, null, 0);
        foreach (var candidate in new[] { Text(item, "exerciseId", "id", "exercise"), Text(item, "name", "exerciseName", "title") })
        {
            if (Guid.TryParse(candidate, out var exerciseId) && allowedIds.Contains(exerciseId))
            {
                exercise = Build(item, exerciseId);
                return true;
            }

            if (TryMatchName(candidate, allowedIds, exerciseNames, out exerciseId))
            {
                exercise = Build(item, exerciseId);
                return true;
            }
        }

        return false;
    }

    private static GeneratedExercise Build(JsonElement item, Guid exerciseId)
    {
        var reps = Integer(item, "targetReps", "reps");
        var duration = Integer(item, "targetDurationSeconds", "durationSeconds");
        return new GeneratedExercise(
            exerciseId,
            Math.Clamp(Integer(item, "sets") ?? 3, 1, 10),
            reps is null ? null : Math.Clamp(reps.Value, 1, 100),
            duration is null ? null : Math.Clamp(duration.Value, 1, 7200),
            Math.Clamp(Integer(item, "restSeconds", "rest") ?? 60, 0, 600));
    }

    private static bool TryMatchName(string? candidate, IReadOnlySet<Guid> allowedIds, IReadOnlyDictionary<string, Guid>? exerciseNames, out Guid exerciseId)
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

    private static bool TryArray(JsonElement element, out List<JsonElement> items, params string[] names)
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

    private static string? Text(JsonElement element, params string[] names)
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

    private static int? Integer(JsonElement element, params string[] names)
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

        return value.Value.ValueKind == JsonValueKind.String && int.TryParse(value.Value.GetString(), out var parsed) ? parsed : null;
    }

    private static bool Flag(JsonElement element, params string[] names)
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

    private static string? ExtractJson(string json)
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
}

public sealed record GeneratedProgram(string Name, IReadOnlyList<GeneratedDay> Days);

public sealed record GeneratedDay(int DayNumber, string Name, bool RestDay, IReadOnlyList<GeneratedExercise> Exercises);

public sealed record GeneratedExercise(Guid ExerciseId, int Sets, int? TargetReps, int? TargetDurationSeconds, int RestSeconds);
