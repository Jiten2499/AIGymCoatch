using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Fitness.Ai;

public static class WorkoutMonthRules
{
    public static Result Validate(GeneratedProgram program, int? daysPerWeek, IReadOnlyDictionary<Guid, string> names, string? avoid)
    {
        if (daysPerWeek is >= 1 and <= 7)
        {
            var training = program.Days.Count(day => !day.RestDay);
            if (Math.Abs((training * 7m / 30m) - daysPerWeek.Value) > 1.25m)
            {
                return Result.Failure(AiErrors.FrequencyMismatch);
            }
        }

        if (Conflicts(program, names, avoid))
        {
            return Result.Failure(AiErrors.RestrictedExercise);
        }

        var weeks = new[] { Signature(program, 1, 7), Signature(program, 8, 14), Signature(program, 15, 21), Signature(program, 22, 30) };
        return weeks.Distinct(StringComparer.Ordinal).Count() >= 2 ? Result.Success() : Result.Failure(AiErrors.NoProgression);
    }

    private static bool Conflicts(GeneratedProgram program, IReadOnlyDictionary<Guid, string> names, string? avoid)
    {
        var phrases = (avoid ?? string.Empty).Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(item => item.Length >= 4).ToArray();
        if (phrases.Length == 0)
        {
            return false;
        }

        return program.Days.SelectMany(day => day.Exercises).Any(item => names.TryGetValue(item.ExerciseId, out var name) && phrases.Any(phrase => name.Contains(phrase, StringComparison.OrdinalIgnoreCase)));
    }

    private static string Signature(GeneratedProgram program, int start, int end) =>
        string.Join('|', program.Days.Where(day => day.DayNumber >= start && day.DayNumber <= end && !day.RestDay).Select(day => string.Join(',', day.Exercises.Select(item => $"{item.ExerciseId:N}:{item.Sets}:{item.TargetReps}"))).Distinct().OrderBy(item => item, StringComparer.Ordinal));
}
