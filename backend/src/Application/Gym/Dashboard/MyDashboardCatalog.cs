using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record DashboardWidgetDefinition(
    string Code,
    string Title,
    string WidgetPermission,
    string? DataPermission,
    string? Feature,
    string? Href);

public sealed record DashboardQuickAction(string Label, string Href, string Permission);

public static class MyDashboardCatalog
{
    public const string OwnerLayout = "owner";
    public const string ReceptionistLayout = "receptionist";
    public const string TrainerLayout = "trainer";
    public const string AccountantLayout = "accountant";
    public const string ManagerLayout = "manager";
    public const string StaffLayout = "staff";

    public static readonly IReadOnlyList<DashboardWidgetDefinition> Widgets =
    [
        Widget("checkins", "Today's check-ins", PermissionCodes.DashboardWidgetCheckIns, PermissionCodes.AttendanceRead, FeatureCodes.Attendance, "/business/members"),
        Widget("new-members", "New members", PermissionCodes.DashboardWidgetNewMembers, PermissionCodes.MemberRead, FeatureCodes.MemberManagement, "/business/members"),
        Widget("expiring", "Memberships expiring", PermissionCodes.DashboardWidgetExpiring, PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement, "/business/memberships"),
        Widget("collections", "Collections", PermissionCodes.DashboardWidgetCollections, PermissionCodes.PaymentRead, FeatureCodes.GymPaymentManagement, "/business/memberships"),
        Widget("follow-ups", "Leads requiring follow-up", PermissionCodes.DashboardWidgetFollowUps, PermissionCodes.CrmRead, FeatureCodes.GymCrm, "/business/leads"),
        Widget("operations", "Today's operations", PermissionCodes.DashboardWidgetOperations, PermissionCodes.AttendanceRead, FeatureCodes.Attendance, "/business/tasks"),
        Widget("sessions", "Today's sessions", PermissionCodes.DashboardWidgetSessions, PermissionCodes.WorkoutRead, FeatureCodes.StaffManagement, "/business/coaches"),
        Widget("assigned-members", "Assigned members", PermissionCodes.DashboardWidgetAssignedMembers, PermissionCodes.MemberRead, FeatureCodes.MemberManagement, "/business/members"),
        Widget("assessments", "Pending assessments", PermissionCodes.DashboardWidgetAssessments, PermissionCodes.FitnessRead, FeatureCodes.StaffManagement, "/business/members"),
        Widget("progress", "Member progress alerts", PermissionCodes.DashboardWidgetProgress, PermissionCodes.FitnessRead, FeatureCodes.StaffManagement, "/business/members"),
        Widget("pt-targets", "PT session targets", PermissionCodes.DashboardWidgetPtTargets, PermissionCodes.WorkoutRead, FeatureCodes.StaffManagement, "/business/coaches"),
        Widget("coach-credits", "Coach credits", PermissionCodes.DashboardWidgetCoachCredits, PermissionCodes.TrainerRead, FeatureCodes.StaffManagement, "/business/coaches"),
        Widget("outstanding", "Outstanding balances", PermissionCodes.DashboardWidgetOutstanding, PermissionCodes.PaymentRead, FeatureCodes.GymPaymentManagement, "/business/memberships"),
        Widget("financials", "Financial summary", PermissionCodes.DashboardWidgetFinancials, PermissionCodes.LedgerRead, FeatureCodes.GymFinancialLedger, "/business/ledger"),
        Widget("reconciliation", "Reconciliation", PermissionCodes.DashboardWidgetReconciliation, PermissionCodes.LedgerRead, FeatureCodes.GymFinancialLedger, "/business/ledger"),
        Widget("payroll", "Payroll", PermissionCodes.DashboardWidgetPayroll, PermissionCodes.PayrollRead, FeatureCodes.GymStaffPayroll, "/business/payroll"),
        Widget("attendance", "Attendance", PermissionCodes.DashboardWidgetAttendance, PermissionCodes.AttendanceRead, FeatureCodes.Attendance, "/business/members"),
        Widget("renewals", "Membership renewals", PermissionCodes.DashboardWidgetRenewals, PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement, "/business/memberships"),
        Widget("trainer-performance", "Trainer performance", PermissionCodes.DashboardWidgetTrainerPerformance, PermissionCodes.TrainerRead, FeatureCodes.StaffManagement, "/business/coaches"),
        Widget("leave-requests", "Staff leave requests", PermissionCodes.DashboardWidgetLeaveRequests, PermissionCodes.LeaveRead, FeatureCodes.StaffLeave, "/business/leaves"),
        Widget("schedule", "Personal schedule", PermissionCodes.DashboardWidgetSchedule, null, null, "/business/leaves"),
        Widget("my-attendance", "My attendance", PermissionCodes.DashboardWidgetMyAttendance, PermissionCodes.AttendanceRead, FeatureCodes.Attendance, null),
        Widget("tasks", "Assigned tasks", PermissionCodes.DashboardWidgetTasks, null, null, "/business/tasks"),
        Widget("leave-status", "Leave status", PermissionCodes.DashboardWidgetLeaveStatus, null, FeatureCodes.StaffLeave, "/business/leaves"),
        Widget("circulars", "Circulars", PermissionCodes.DashboardWidgetCirculars, PermissionCodes.CommunicationRead, FeatureCodes.GymCommunications, "/business/communications"),
        Widget("notifications", "Notifications", PermissionCodes.DashboardWidgetNotifications, PermissionCodes.NotificationRead, null, null),
        Widget("chats", "Unread chats", PermissionCodes.DashboardWidgetChats, PermissionCodes.ChatRead, FeatureCodes.GymChat, "/business/chats"),
    ];

    public static readonly IReadOnlyList<DashboardQuickAction> Actions =
    [
        new("Add a member", "/business/members", PermissionCodes.MemberCreate),
        new("Collect a payment", "/business/memberships", PermissionCodes.PaymentCreate),
        new("Follow up a lead", "/business/leads", PermissionCodes.CrmUpdate),
        new("Review tasks", "/business/tasks", PermissionCodes.LeaveApprove),
        new("Open chats", "/business/chats", PermissionCodes.ChatRead),
    ];

    private static readonly Dictionary<string, string[]> Order = new(StringComparer.Ordinal)
    {
        [ReceptionistLayout] = ["checkins", "new-members", "expiring", "collections", "follow-ups", "operations"],
        [TrainerLayout] = ["sessions", "assigned-members", "assessments", "progress", "pt-targets", "coach-credits", "schedule"],
        [AccountantLayout] = ["collections", "outstanding", "financials", "reconciliation", "payroll"],
        [ManagerLayout] = ["operations", "attendance", "renewals", "follow-ups", "trainer-performance", "leave-requests", "checkins"],
        [StaffLayout] = ["schedule", "my-attendance", "tasks", "leave-status", "circulars", "notifications", "chats"],
    };

    public static string LayoutFor(IReadOnlyCollection<string> roles)
    {
        if (roles.Contains(RoleNames.GymOwner, StringComparer.Ordinal) || roles.Contains(RoleNames.BusinessAdmin, StringComparer.Ordinal))
        {
            return OwnerLayout;
        }

        if (roles.Contains(RoleNames.GymManager, StringComparer.Ordinal))
        {
            return ManagerLayout;
        }

        if (roles.Contains(RoleNames.Accountant, StringComparer.Ordinal))
        {
            return AccountantLayout;
        }

        if (roles.Contains(RoleNames.Receptionist, StringComparer.Ordinal))
        {
            return ReceptionistLayout;
        }

        if (roles.Contains(RoleNames.Trainer, StringComparer.Ordinal))
        {
            return TrainerLayout;
        }

        return StaffLayout;
    }

    public static bool WidgetVisibilityConfigured(IReadOnlySet<string> permissions) =>
        permissions.Any(code => code.StartsWith("DASHBOARD_WIDGET_", StringComparison.Ordinal));

    public static IReadOnlyList<DashboardWidgetDefinition> Visible(
        string layout,
        IReadOnlySet<string> permissions,
        IReadOnlySet<string> enabledFeatures)
    {
        var configured = WidgetVisibilityConfigured(permissions);
        Order.TryGetValue(layout, out var preferred);
        var ordered = Arrange(preferred, Select(permissions, enabledFeatures, configured, true), configured);
        if (ordered.Count == 0 && !configured)
        {
            ordered = Arrange(preferred, Select(permissions, enabledFeatures, false, false), false);
        }

        return ordered;
    }

    private static IReadOnlyList<DashboardWidgetDefinition> Arrange(
        string[]? preferred,
        Dictionary<string, DashboardWidgetDefinition> allowed,
        bool includeExtras)
    {
        if (preferred is null)
        {
            return allowed.Values.ToArray();
        }

        var ordered = preferred.Where(allowed.ContainsKey).Select(code => allowed[code]).ToList();
        if (includeExtras)
        {
            ordered.AddRange(allowed.Values.Where(widget => !preferred.Contains(widget.Code, StringComparer.Ordinal)));
        }

        return ordered;
    }

    public static bool PaymentsAreShared(IEnumerable<string> paymentScopes) =>
        paymentScopes.Contains(AccessScopes.All, StringComparer.Ordinal);

    private static Dictionary<string, DashboardWidgetDefinition> Select(
        IReadOnlySet<string> permissions,
        IReadOnlySet<string> enabledFeatures,
        bool configured,
        bool requireFeatures) =>
        Widgets.Where(widget => CanSee(widget, permissions, enabledFeatures, configured, requireFeatures))
            .ToDictionary(widget => widget.Code, StringComparer.Ordinal);

    private static bool CanSee(DashboardWidgetDefinition widget, IReadOnlySet<string> permissions, IReadOnlySet<string> enabledFeatures, bool configured, bool requireFeatures) =>
        (!configured || permissions.Contains(widget.WidgetPermission))
        && (widget.DataPermission is null || permissions.Contains(widget.DataPermission))
        && (!requireFeatures || widget.Feature is null || enabledFeatures.Contains(widget.Feature));

    private static DashboardWidgetDefinition Widget(string code, string title, string widgetPermission, string? dataPermission, string? feature, string? href) =>
        new(code, title, widgetPermission, dataPermission, feature, href);
}
