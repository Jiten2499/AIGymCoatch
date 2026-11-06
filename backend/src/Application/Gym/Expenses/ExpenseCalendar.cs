namespace LifeFitAI.Application.Gym.Expenses;

public static class ExpenseCalendar
{
    public static TimeZoneInfo Zone(string? timeZoneId) =>
        timeZoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;

    public static DateOnly LocalDate(DateTime utcNow, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        return DateOnly.FromDateTime(local);
    }

    public static DateOnly WeekStart(DateOnly localDate)
    {
        var offset = ((int)localDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return localDate.AddDays(-offset);
    }

    public static DateOnly MonthStart(DateOnly localDate) => new(localDate.Year, localDate.Month, 1);
}
