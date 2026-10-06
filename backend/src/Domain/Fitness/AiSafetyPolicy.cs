namespace LifeFitAI.Domain.Fitness;

public static class AiSafetyPolicy
{
    public const string Refusal = "I can help with general fitness and nutrition. I cannot diagnose conditions, recommend medication or supplements, or suggest extreme diets or dangerous training.";

    private static readonly string[] Blocked =
    [
        "ignore previous instructions",
        "ignore all previous",
        "reveal the system prompt",
        "reveal your prompt",
        "system prompt",
        "diagnos",
        "prescri",
        "medication",
        "anabolic",
        "steroid cycle",
        "eating disorder",
        "purge",
        "starve",
        "500 calorie",
        "dangerous supplement",
    ];

    public static bool Allows(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.ToLowerInvariant();
        return !Blocked.Any(value.Contains);
    }
}
