using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Authorization;

public sealed record AccessAction(string Code, string Action, string Label);

public sealed record AccessModuleDefinition(string Key, string Name, bool Scoped, IReadOnlyList<AccessAction> Actions);

public sealed record AccessScopeInput(string Resource, string Scope);

public static class AccessRoleCatalog
{
    public static readonly string[] SensitiveCodes =
    [
        PermissionCodes.UserDelete,
        PermissionCodes.RoleAssign,
        PermissionCodes.PaymentVoid,
        PermissionCodes.PayrollPay,
        PermissionCodes.LicenseManage,
        PermissionCodes.TenantMemberRemove,
        PermissionCodes.ConfigurationManage,
        PermissionCodes.MemberDelete,
    ];

    public static IReadOnlyList<AccessModuleDefinition> Modules { get; } =
    [
        Module("dashboard", "Dashboard", false, View(PermissionCodes.ReportRead, "View dashboard")),
        Module("dashboard-widgets", "Dashboard Widgets", false,
            View(PermissionCodes.DashboardWidgetCheckIns, "Today's check-ins"),
            View(PermissionCodes.DashboardWidgetNewMembers, "New members"),
            View(PermissionCodes.DashboardWidgetExpiring, "Memberships expiring"),
            View(PermissionCodes.DashboardWidgetCollections, "Collections"),
            View(PermissionCodes.DashboardWidgetFollowUps, "Lead follow-ups"),
            View(PermissionCodes.DashboardWidgetOperations, "Today's operations"),
            View(PermissionCodes.DashboardWidgetQuickActions, "Quick actions"),
            View(PermissionCodes.DashboardWidgetSessions, "Today's sessions"),
            View(PermissionCodes.DashboardWidgetAssignedMembers, "Assigned members"),
            View(PermissionCodes.DashboardWidgetAssessments, "Pending assessments"),
            View(PermissionCodes.DashboardWidgetProgress, "Progress alerts"),
            View(PermissionCodes.DashboardWidgetPtTargets, "PT session targets"),
            View(PermissionCodes.DashboardWidgetCoachCredits, "Coach credits"),
            View(PermissionCodes.DashboardWidgetOutstanding, "Outstanding balances"),
            View(PermissionCodes.DashboardWidgetFinancials, "Financial summary"),
            View(PermissionCodes.DashboardWidgetReconciliation, "Reconciliation"),
            View(PermissionCodes.DashboardWidgetPayroll, "Payroll"),
            View(PermissionCodes.DashboardWidgetAttendance, "Attendance"),
            View(PermissionCodes.DashboardWidgetRenewals, "Membership renewals"),
            View(PermissionCodes.DashboardWidgetTrainerPerformance, "Trainer performance"),
            View(PermissionCodes.DashboardWidgetLeaveRequests, "Leave requests"),
            View(PermissionCodes.DashboardWidgetSchedule, "Personal schedule"),
            View(PermissionCodes.DashboardWidgetMyAttendance, "My attendance"),
            View(PermissionCodes.DashboardWidgetTasks, "Assigned tasks"),
            View(PermissionCodes.DashboardWidgetLeaveStatus, "Leave status"),
            View(PermissionCodes.DashboardWidgetCirculars, "Circulars"),
            View(PermissionCodes.DashboardWidgetNotifications, "Notifications"),
            View(PermissionCodes.DashboardWidgetChats, "Unread chats")),
        Module("members", "Members", true,
            View(PermissionCodes.MemberRead, "View members"),
            Create(PermissionCodes.MemberCreate, "Create members"),
            Edit(PermissionCodes.MemberUpdate, "Edit members"),
            Delete(PermissionCodes.MemberDelete, "Delete members")),
        Module("memberships", "Memberships", false,
            View(PermissionCodes.MembershipRead, "View memberships"),
            View(PermissionCodes.MembershipPlanRead, "View plans"),
            Create(PermissionCodes.MembershipCreate, "Create memberships"),
            Edit(PermissionCodes.MembershipRenew, "Renew memberships"),
            Edit(PermissionCodes.MembershipPlanManage, "Edit plans"),
            Approve(PermissionCodes.MembershipFreeze, "Freeze memberships"),
            Delete(PermissionCodes.MembershipCancel, "Cancel memberships")),
        Module("payments", "Payments", true,
            View(PermissionCodes.PaymentRead, "View payments"),
            View(PermissionCodes.LedgerRead, "View ledger"),
            Create(PermissionCodes.PaymentCreate, "Create payments"),
            Create(PermissionCodes.LedgerCreate, "Create ledger entries"),
            Delete(PermissionCodes.PaymentVoid, "Void payments"),
            Delete(PermissionCodes.LedgerVoid, "Void ledger entries")),
        Module("attendance", "Attendance", true,
            View(PermissionCodes.AttendanceRead, "View attendance"),
            Create(PermissionCodes.AttendanceCheckIn, "Check in"),
            Edit(PermissionCodes.AttendanceCheckOut, "Check out")),
        Module("crm", "Leads & CRM", false,
            View(PermissionCodes.CrmRead, "View leads"),
            Create(PermissionCodes.CrmCreate, "Create leads"),
            Edit(PermissionCodes.CrmUpdate, "Edit leads"),
            Assign(PermissionCodes.CrmConvert, "Convert leads"),
            Delete(PermissionCodes.CrmLost, "Mark leads lost")),
        Module("pt", "PT Sessions", true,
            View(PermissionCodes.WorkoutRead, "View sessions"),
            View(PermissionCodes.FitnessRead, "View fitness records"),
            Edit(PermissionCodes.WorkoutManage, "Edit sessions"),
            Edit(PermissionCodes.FitnessManage, "Edit fitness records")),
        Module("trainers", "Trainers", false,
            View(PermissionCodes.TrainerRead, "View trainers"),
            Edit(PermissionCodes.TrainerUpdate, "Edit trainers")),
        Module("reports", "Reports", false, View(PermissionCodes.ReportRead, "View reports")),
        Module("expenses", "Expenses", false,
            View(PermissionCodes.ExpenseRead, "View expenses"),
            Create(PermissionCodes.ExpenseCreate, "Create expenses"),
            Edit(PermissionCodes.ExpenseUpdate, "Edit expenses"),
            Edit(PermissionCodes.ExpenseCategoryManage, "Edit categories"),
            Delete(PermissionCodes.ExpenseVoid, "Void expenses")),
        Module("payroll", "Payroll", false,
            View(PermissionCodes.PayrollRead, "View payroll"),
            Create(PermissionCodes.PayrollPrepare, "Prepare payroll"),
            Approve(PermissionCodes.PayrollApprove, "Approve payroll"),
            Edit(PermissionCodes.PayrollPay, "Pay payroll"),
            View(PermissionCodes.LeaveRead, "View leave"),
            Approve(PermissionCodes.LeaveApprove, "Approve leave")),
        Module("staff", "Staff Management", false,
            View(PermissionCodes.StaffRead, "View staff"),
            View(PermissionCodes.UserRead, "View accounts"),
            View(PermissionCodes.TenantMemberRead, "View workspace access"),
            Create(PermissionCodes.StaffCreate, "Add staff"),
            Create(PermissionCodes.UserCreate, "Create accounts"),
            Edit(PermissionCodes.StaffUpdate, "Edit staff"),
            Edit(PermissionCodes.UserUpdate, "Edit accounts"),
            Assign(PermissionCodes.TenantMemberInvite, "Grant workspace access"),
            Delete(PermissionCodes.StaffRemove, "Remove staff"),
            Delete(PermissionCodes.TenantMemberRemove, "Remove workspace access")),
        Module("chat", "Chat", false,
            View(PermissionCodes.ChatRead, "View chat"),
            Create(PermissionCodes.ChatSend, "Send messages"),
            Edit(PermissionCodes.ChatConfigure, "Configure chat")),
        Module("configuration", "Configuration", false,
            View(PermissionCodes.ConfigurationRead, "View configuration"),
            View(PermissionCodes.RoleRead, "View roles"),
            Edit(PermissionCodes.ConfigurationManage, "Edit configuration"),
            Assign(PermissionCodes.RoleAssign, "Assign roles")),
    ];

    public static IReadOnlySet<string> Codes { get; } = Modules.SelectMany(module => module.Actions).Select(action => action.Code).ToHashSet(StringComparer.Ordinal);

    public static string Label(string roleName) => roleName switch
    {
        RoleNames.GymOwner => "Business Owner",
        RoleNames.BusinessAdmin => "Business Admin",
        RoleNames.GymManager => "Manager",
        _ => roleName,
    };

    public static string Summary(IReadOnlyCollection<string> codes)
    {
        var names = Modules.Where(module => module.Actions.Any(action => codes.Contains(action.Code))).Select(module => module.Name).ToArray();
        return names.Length == 0 ? "No permissions" : string.Join(", ", names);
    }

    public static string FallbackScope(string roleName) => roleName switch
    {
        RoleNames.Trainer => AccessScopes.Assigned,
        RoleNames.Member or RoleNames.PersonalMember => AccessScopes.Own,
        _ => AccessScopes.All,
    };

    private static AccessModuleDefinition Module(string key, string name, bool scoped, params AccessAction[] actions) =>
        new(key, name, scoped, actions);

    private static AccessAction View(string code, string label) => new(code, "View", label);

    private static AccessAction Create(string code, string label) => new(code, "Create", label);

    private static AccessAction Edit(string code, string label) => new(code, "Edit", label);

    private static AccessAction Delete(string code, string label) => new(code, "Delete", label);

    private static AccessAction Approve(string code, string label) => new(code, "Approve", label);

    private static AccessAction Assign(string code, string label) => new(code, "Assign", label);
}

public static class AccessRoleRules
{
    public static bool OwnerAssignmentAllowed(string targetRoleName, IReadOnlyCollection<string> actorRoleNames) =>
        targetRoleName != RoleNames.GymOwner || actorRoleNames.Contains(RoleNames.GymOwner, StringComparer.Ordinal);

    public static bool CanGrant(IReadOnlyCollection<string> held, IReadOnlyCollection<string> requested) =>
        requested.All(code => held.Contains(code, StringComparer.Ordinal) && AccessRoleCatalog.Codes.Contains(code));

    public static bool IsManaged(Role role, Guid tenantId) =>
        role.TenantId == tenantId || (role.TenantId is null && RoleNames.BusinessTemplates.Contains(role.Name, StringComparer.Ordinal));
}
