using System.Text.Json.Serialization;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record MetricChange(int Current, int Previous, int Change, decimal? ChangePercent, string Direction);

public sealed record InsightWindow(DateOnly From, DateOnly To, int Days);

public sealed record BusinessInsightFacts(
    InsightWindow Period,
    InsightWindow? Comparison,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TotalMembers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ActiveMembers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MetricChange? NewMembers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MetricChange? CheckIns,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? AverageDailyCheckIns,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CheckInsToday,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ActiveMemberships,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ExpiringIn7Days,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MetricChange? NewMemberships,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MetricChange? RenewedMemberships,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MetricChange? ExpiredMemberships)
{
    public bool HasActivity() =>
        TotalMembers is > 0 || ActiveMembers is > 0 || CheckInsToday is > 0 || ActiveMemberships is > 0 || ExpiringIn7Days is > 0
        || Has(NewMembers) || Has(CheckIns) || Has(NewMemberships) || Has(RenewedMemberships) || Has(ExpiredMemberships);

    private static bool Has(MetricChange? metric) => metric is not null && (metric.Current > 0 || metric.Previous > 0);
}

public static class InsightMath
{
    public static InsightWindow Window(DateOnly from, DateOnly to) =>
        new(from, to, to.DayNumber - from.DayNumber + 1);

    public static InsightWindow Previous(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        var previousTo = from.AddDays(-1);
        return new InsightWindow(previousTo.AddDays(1 - days), previousTo, days);
    }

    public static MetricChange Compare(int current, int? previous)
    {
        if (previous is null)
        {
            return new MetricChange(current, 0, 0, null, "Unavailable");
        }

        var change = current - previous.Value;
        decimal? percent = previous.Value == 0 ? null : Math.Round(change * 100m / previous.Value, 2, MidpointRounding.AwayFromZero);
        var direction = change > 0 ? "Up" : change < 0 ? "Down" : "Flat";
        return new MetricChange(current, previous.Value, change, percent, direction);
    }

    public static int Sum(ResolvedAnalytics calendar, IReadOnlyList<DateCount> counts, string key) =>
        calendar.Series(key, key, calendar.Rollup(counts)).Points.Sum(point => point.Value);

    public static decimal AverageDaily(int total, int days) =>
        Math.Round(total / (decimal)Math.Max(days, 1), 2, MidpointRounding.AwayFromZero);
}
