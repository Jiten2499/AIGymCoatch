using System.Text.Json;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Fitness.Ai;

public static class WorkoutPlanParser
{
    public static Result<GeneratedProgram> ParseMonth(string? json, IReadOnlySet<Guid> allowedIds, IReadOnlyDictionary<string, Guid>? exerciseNames = null)
    {
        var parsed = Parse(json, allowedIds, exerciseNames, 31);
        if (parsed.IsFailure)
        {
            return parsed;
        }

        var days = parsed.Value.Days.OrderBy(day => day.DayNumber).ToArray();
        var sequential = days.Length == 30 && days.Select((day, index) => day.DayNumber == index + 1).All(match => match);
        var volume = sequential && days.Where(day => !day.RestDay).SelectMany(day => day.Exercises).All(item => item.TargetReps is not null || item.TargetDurationSeconds is not null);
        return sequential && volume && days.Any(day => day.RestDay)
            ? Result.Success(new GeneratedProgram(parsed.Value.Name, days))
            : Result.Failure<GeneratedProgram>(AiErrors.InvalidOutput);
    }

    public static Result<GeneratedProgram> Parse(string? json, IReadOnlySet<Guid> allowedIds, IReadOnlyDictionary<string, Guid>? exerciseNames = null, int maxDays = 14)
    {
        if (string.IsNullOrWhiteSpace(json) || allowedIds.Count == 0 || PlanJson.Extract(json) is not string text)
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
            if (root.ValueKind != JsonValueKind.Object || !PlanJson.Array(root, out var dayElements, "days", "workouts", "schedule") || dayElements.Count < 1 || dayElements.Count > maxDays)
            {
                return Result.Failure<GeneratedProgram>(AiErrors.InvalidOutput);
            }

            var days = new List<GeneratedDay>();
            for (var index = 0; index < dayElements.Count; index++)
            {
                if (!TryDay(dayElements[index], index, allowedIds, exerciseNames, maxDays, out var day))
                {
                    return Result.Failure<GeneratedProgram>(AiErrors.InvalidOutput);
                }

                days.Add(day);
            }

            if (days.Select(day => day.DayNumber).Distinct().Count() != days.Count)
            {
                return Result.Failure<GeneratedProgram>(AiErrors.InvalidOutput);
            }

            var name = PlanJson.Text(root, "name", "title", "programName") ?? "Workout plan";
            return Result.Success(new GeneratedProgram(name.Trim(), days));
        }
    }

    private static bool TryDay(JsonElement day, int index, IReadOnlySet<Guid> allowedIds, IReadOnlyDictionary<string, Guid>? exerciseNames, int maxDays, out GeneratedDay parsed)
    {
        parsed = new GeneratedDay(0, string.Empty, false, []);
        if (day.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var number = PlanJson.Integer(day, "dayNumber", "day", "number") ?? index + 1;
        if (number < 1 || number > maxDays)
        {
            return false;
        }

        var rest = PlanJson.Flag(day, "restDay", "rest");
        var exercises = new List<GeneratedExercise>();
        if (!rest)
        {
            foreach (var keys in new[] { new[] { "warmup", "warmUp" }, ["exercises", "items", "movements"], ["cardio"], ["cooldown", "coolDown"] })
            {
                if (!PlanJson.Array(day, out var items, keys))
                {
                    continue;
                }

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

        var name = (PlanJson.Text(day, "name", "title") ?? $"Day {number}").Trim();
        var description = PlanJson.Text(day, "recoveryInstructions", "description", "workoutDescription");
        parsed = new GeneratedDay(number, name.Length <= 200 ? name : name[..200], rest, exercises, string.IsNullOrWhiteSpace(description) ? null : description.Trim());
        return true;
    }

    private static bool TryResolve(JsonElement item, IReadOnlySet<Guid> allowedIds, IReadOnlyDictionary<string, Guid>? exerciseNames, out GeneratedExercise exercise)
    {
        exercise = new GeneratedExercise(Guid.Empty, 0, null, null, null, 0, null);
        foreach (var candidate in new[] { PlanJson.Text(item, "exerciseId", "id", "exercise"), PlanJson.Text(item, "name", "exerciseName", "title") })
        {
            if (Guid.TryParse(candidate, out var exerciseId) && allowedIds.Contains(exerciseId))
            {
                exercise = Build(item, exerciseId);
                return true;
            }

            if (PlanJson.Match(candidate, allowedIds, exerciseNames, out exerciseId))
            {
                exercise = Build(item, exerciseId);
                return true;
            }
        }

        return false;
    }

    private static GeneratedExercise Build(JsonElement item, Guid exerciseId)
    {
        var reps = PlanJson.Integer(item, "targetReps", "reps");
        var duration = PlanJson.Integer(item, "targetDurationSeconds", "durationSeconds", "duration");
        var weight = PlanJson.Decimal(item, "targetWeight", "weight", "load");
        var notes = PlanJson.Text(item, "notes", "cues", "instruction", "instructions") ?? string.Empty;
        var rpe = PlanJson.Decimal(item, "rpe", "intensity");
        if (rpe is not null)
        {
            notes = $"RPE {Math.Clamp(rpe.Value, 1, 10):0.#}. {notes}";
        }

        var repsText = PlanJson.Text(item, "targetReps", "reps");
        if (repsText is not null && repsText.Contains('-', StringComparison.Ordinal))
        {
            notes = $"{repsText}. {notes}";
        }

        notes = notes.Trim();
        return new GeneratedExercise(
            exerciseId,
            Math.Clamp(PlanJson.Integer(item, "sets") ?? 3, 1, 10),
            reps is null ? null : Math.Clamp(reps.Value, 1, 100),
            duration is null ? null : Math.Clamp(duration.Value, 1, 7200),
            weight is null ? null : Math.Clamp(weight.Value, 0, 1000),
            Math.Clamp(PlanJson.Integer(item, "restSeconds", "rest") ?? 60, 0, 600),
            notes.Length == 0 ? null : notes[..Math.Min(notes.Length, 240)]);
    }
}

public sealed record GeneratedProgram(string Name, IReadOnlyList<GeneratedDay> Days);

public sealed record GeneratedDay(int DayNumber, string Name, bool RestDay, IReadOnlyList<GeneratedExercise> Exercises, string? Description = null);

public sealed record GeneratedExercise(Guid ExerciseId, int Sets, int? TargetReps, int? TargetDurationSeconds, decimal? TargetWeight, int RestSeconds, string? Notes);
