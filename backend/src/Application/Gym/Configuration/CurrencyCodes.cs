using System.Globalization;

namespace LifeFitAI.Application.Gym.Configuration;

public static class CurrencyCodes
{
    public static IReadOnlyList<CurrencyOption> Options { get; } = Build();

    private static readonly HashSet<string> Known = Options.Select(option => option.Code).ToHashSet(StringComparer.Ordinal);

    public static bool IsKnown(string code) => Known.Contains(code.Trim().ToUpperInvariant());

    private static IReadOnlyList<CurrencyOption> Build()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["USD"] = "US Dollar",
            ["INR"] = "Indian Rupee",
        };
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                var code = region.ISOCurrencySymbol.Trim().ToUpperInvariant();
                if (code.Length == 3 && code.All(char.IsLetter))
                {
                    names.TryAdd(code, region.CurrencyEnglishName);
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return names
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new CurrencyOption(item.Key, item.Value))
            .ToArray();
    }
}
