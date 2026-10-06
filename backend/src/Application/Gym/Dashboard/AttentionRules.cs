namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record AttentionItem(string Type, string Priority, string Title, string Description, int? Value, string? Action);

public static class AttentionRules
{
    // A 20% drop versus the previous equal-length period is worth a review. 40% is high priority.
    public const decimal DeclinePercent = -20m;
    public const decimal SharpDeclinePercent = -40m;

    // One expiry is enough to surface. Ten or more is high priority.
    public const int HighCount = 10;

    // Growth of 10% or more is called out as a positive item.
    public const decimal GrowthPercent = 10m;

    public static IReadOnlyList<AttentionItem> Evaluate(BusinessInsightFacts facts)
    {
        var items = new List<AttentionItem>();
        if (facts.ExpiringIn7Days is int expiring && expiring >= 1)
        {
            items.Add(Item("MembershipExpiry", expiring >= HighCount ? "High" : "Medium", "Memberships expiring soon", $"{expiring} memberships expire in the next 7 days.", expiring, "Review memberships"));
        }

        if (facts.ExpiredMemberships?.Current is int expired && expired >= 1)
        {
            items.Add(Item("MembershipExpired", expired >= HighCount ? "High" : "Medium", "Memberships expired", $"{expired} memberships expired in the selected period.", expired, "Review memberships"));
        }

        AddDecline(items, facts.CheckIns, "AttendanceDecline", "Attendance decreased", "Check-ins are");
        AddDecline(items, facts.NewMembers, "MemberGrowthDecline", "Member growth declined", "New members are");
        if (facts.NewMembers?.ChangePercent is decimal growth && growth >= GrowthPercent)
        {
            items.Add(Item("MemberGrowth", "Low", "Member growth increased", $"{Format(growth)} compared with the previous period.", facts.NewMembers.Change, null));
        }

        return items
            .OrderBy(item => item.Priority == "High" ? 0 : item.Priority == "Medium" ? 1 : 2)
            .ToArray();
    }

    private static void AddDecline(List<AttentionItem> items, MetricChange? metric, string type, string title, string subject)
    {
        if (metric?.ChangePercent is not decimal percent || percent > DeclinePercent)
        {
            return;
        }

        var priority = percent <= SharpDeclinePercent ? "High" : "Medium";
        items.Add(Item(type, priority, title, $"{subject} {Format(Math.Abs(percent))} lower than the previous period.", metric.Change, null));
    }

    private static AttentionItem Item(string type, string priority, string title, string description, int? value, string? action) =>
        new(type, priority, title, description, value, action);

    private static string Format(decimal percent) => percent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
