namespace LifeFitAI.Application.Gym.Dashboard;

public readonly record struct DashboardPeriod(
    DateOnly Today,
    DateOnly MonthStart,
    DateOnly ExpiringThrough7Days,
    DateOnly ExpiringThrough30Days,
    DateTime TodayStartUtc,
    DateTime TomorrowStartUtc)
{
    public static DashboardPeriod For(DateTime utcNow, TimeZoneInfo timeZone)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
        var today = DateOnly.FromDateTime(local);
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified), timeZone);
        return new(
            today,
            new DateOnly(local.Year, local.Month, 1),
            today.AddDays(7),
            today.AddDays(30),
            todayStartUtc,
            todayStartUtc.AddDays(1));
    }
}

public readonly record struct KpiScope(bool Members, bool Attendance, bool Memberships, bool People)
{
    public bool Any => Members || Attendance || Memberships || People;
}
