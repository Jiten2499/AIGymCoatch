using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Dashboard;

public enum AnalyticsPeriod
{
    Day = 1,
    Week = 2,
    Month = 3,
}

public sealed record AnalyticsPoint(DateOnly PeriodStart, DateOnly PeriodEnd, string Label, int Value);

public sealed record AnalyticsSeries(string Key, string Label, IReadOnlyList<AnalyticsPoint> Points);

public sealed record AnalyticsReport(DateOnly From, DateOnly To, AnalyticsPeriod Period, IReadOnlyList<AnalyticsSeries> Series);

public readonly record struct DateCount(DateOnly Date, int Count);

public readonly record struct MembershipActivityCount(DateOnly Date, string Action, int Count);

public readonly record struct MembershipSaleCount(Guid PlanId, string PackageName, int Sold);

public sealed record AnalyticsBucket(DateOnly Start, DateOnly End, string Label);

public sealed class ResolvedAnalytics
{
    public const int MaxDailyDays = 92;
    public const int MaxWeeklyDays = 104 * 7;
    public const int MaxMonthlyDays = 36 * 31;

    private readonly Dictionary<DateOnly, DateOnly> _bucketByDate;

    private ResolvedAnalytics(DateOnly from, DateOnly to, AnalyticsPeriod period, TimeZoneInfo timeZone, DateTime fromUtc, DateTime toUtc, IReadOnlyList<AnalyticsBucket> buckets)
    {
        From = from;
        To = to;
        Period = period;
        TimeZone = timeZone;
        FromUtc = fromUtc;
        ToUtc = toUtc;
        Buckets = buckets;
        _bucketByDate = new Dictionary<DateOnly, DateOnly>();
        foreach (var bucket in buckets)
        {
            for (var date = bucket.Start; date <= bucket.End; date = date.AddDays(1))
            {
                _bucketByDate[date] = bucket.Start;
            }
        }
    }

    public DateOnly From { get; }

    public DateOnly To { get; }

    public AnalyticsPeriod Period { get; }

    public TimeZoneInfo TimeZone { get; }

    public DateTime FromUtc { get; }

    public DateTime ToUtc { get; }

    public IReadOnlyList<AnalyticsBucket> Buckets { get; }

    public static Result<ResolvedAnalytics> Resolve(DateOnly from, DateOnly to, AnalyticsPeriod period, TimeZoneInfo timeZone)
    {
        if (!Enum.IsDefined(period))
        {
            return Result.Failure<ResolvedAnalytics>(GymErrors.AnalyticsRangeInvalid);
        }

        if (from > to)
        {
            return Result.Failure<ResolvedAnalytics>(GymErrors.AnalyticsRangeInvalid);
        }

        var days = to.DayNumber - from.DayNumber + 1;
        var limit = period switch
        {
            AnalyticsPeriod.Day => MaxDailyDays,
            AnalyticsPeriod.Week => MaxWeeklyDays,
            AnalyticsPeriod.Month => MaxMonthlyDays,
            _ => 0,
        };
        if (days > limit)
        {
            return Result.Failure<ResolvedAnalytics>(GymErrors.AnalyticsRangeInvalid);
        }

        try
        {
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), timeZone);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), timeZone);
            return Result.Success(new ResolvedAnalytics(from, to, period, timeZone, fromUtc, toUtc, Build(from, to, period)));
        }
        catch (ArgumentException)
        {
            return Result.Failure<ResolvedAnalytics>(GymErrors.AnalyticsRangeInvalid);
        }
    }

    public DateOnly BucketStart(DateOnly date) => _bucketByDate[date];

    public AnalyticsSeries Series(string key, string label, IReadOnlyDictionary<DateOnly, int> totals) =>
        new(key, label, Buckets.Select(bucket => new AnalyticsPoint(bucket.Start, bucket.End, bucket.Label, totals.GetValueOrDefault(bucket.Start))).ToArray());

    public Dictionary<DateOnly, int> Rollup(IEnumerable<DateCount> counts)
    {
        var totals = new Dictionary<DateOnly, int>();
        foreach (var count in counts)
        {
            if (count.Date < From || count.Date > To)
            {
                continue;
            }

            var start = BucketStart(count.Date);
            totals[start] = totals.GetValueOrDefault(start) + count.Count;
        }

        return totals;
    }

    private static IReadOnlyList<AnalyticsBucket> Build(DateOnly from, DateOnly to, AnalyticsPeriod period)
    {
        var buckets = new List<AnalyticsBucket>();
        var cursor = from;
        while (cursor <= to)
        {
            var end = period switch
            {
                AnalyticsPeriod.Day => cursor,
                AnalyticsPeriod.Week => WeekEnd(cursor),
                _ => new DateOnly(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month)),
            };
            if (end > to)
            {
                end = to;
            }

            var label = period == AnalyticsPeriod.Month ? $"{cursor:yyyy-MM}" : $"{cursor:yyyy-MM-dd}";
            buckets.Add(new AnalyticsBucket(cursor, end, label));
            cursor = end.AddDays(1);
        }

        return buckets;
    }

    private static DateOnly WeekEnd(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(6 - daysFromMonday);
    }
}
