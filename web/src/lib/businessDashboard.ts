import { ApiError } from "./api";

export type DashboardCapability = {
  code: string;
  enabled: boolean;
};

export type BusinessDashboard = {
  tenant: {
    id: string;
    name: string;
    tenantType: string;
    status: string;
    logoUrl: string | null;
    timeZone: string;
    currentDate: string;
  };
  user: {
    displayName: string;
    roles: string[];
  };
  capabilities: DashboardCapability[];
  summary: DashboardSummary;
};

export type DashboardSummary = {
  members: { total: number; active: number; newThisMonth: number } | null;
  attendance: { checkInsToday: number } | null;
  memberships: { active: number; expiringIn7Days: number; expiringIn30Days: number; expired: number } | null;
  people: { activeTrainers: number; activeStaff: number } | null;
};

export type MetricLine = {
  label: string;
  value: number;
};

export type DashboardWidget = {
  code: string;
  title: string;
  body: string;
};

export const dashboardWidgets: DashboardWidget[] = [
  { code: "Members", title: "Members", body: "Member totals will appear here." },
  { code: "Attendance", title: "Attendance", body: "Attendance totals will appear here." },
  { code: "Memberships", title: "Memberships", body: "Membership totals will appear here." },
  { code: "Fitness", title: "Fitness", body: "Fitness activity will appear here." },
  { code: "Trainers", title: "Trainers", body: "Trainer activity will appear here." },
  { code: "AI", title: "AI", body: "AI activity will appear here." },
  { code: "Reports", title: "Reports", body: "Reports will appear here." },
  { code: "Challenges", title: "Challenges", body: "Challenges will appear here." },
];

export type DashboardProblem = "suspended" | "unauthorized" | "unavailable" | "error";

export function summaryHighlights(summary: DashboardSummary): MetricLine[] {
  const lines: MetricLine[] = [];
  if (summary.members) {
    lines.push({ label: "Active members", value: summary.members.active });
  }

  if (summary.attendance) {
    lines.push({ label: "Check-ins today", value: summary.attendance.checkInsToday });
  }

  if (summary.memberships) {
    lines.push({ label: "Active memberships", value: summary.memberships.active });
  }

  if (summary.people) {
    lines.push({ label: "Active trainers", value: summary.people.activeTrainers }, { label: "Active staff", value: summary.people.activeStaff });
  }

  return lines;
}

export function widgetMetrics(code: string, summary: DashboardSummary): MetricLine[] | "placeholder" | "unavailable" {
  if (code === "Members") {
    return summary.members
      ? [
          { label: "Total", value: summary.members.total },
          { label: "Active", value: summary.members.active },
          { label: "New this month", value: summary.members.newThisMonth },
        ]
      : "unavailable";
  }

  if (code === "Attendance") {
    return summary.attendance ? [{ label: "Check-ins today", value: summary.attendance.checkInsToday }] : "unavailable";
  }

  if (code === "Memberships") {
    return summary.memberships
      ? [
          { label: "Active", value: summary.memberships.active },
          { label: "Expiring in 7 days", value: summary.memberships.expiringIn7Days },
          { label: "Expiring in 30 days", value: summary.memberships.expiringIn30Days },
          { label: "Expired", value: summary.memberships.expired },
        ]
      : "unavailable";
  }

  if (code === "Trainers") {
    return summary.people
      ? [
          { label: "Active trainers", value: summary.people.activeTrainers },
          { label: "Active staff", value: summary.people.activeStaff },
        ]
      : "unavailable";
  }

  return "placeholder";
}

export function visibleWidgets(capabilities: DashboardCapability[]): DashboardWidget[] {
  const enabled = new Set(capabilities.filter((capability) => capability.enabled).map((capability) => capability.code));
  return dashboardWidgets.filter((widget) => enabled.has(widget.code));
}

export function dashboardProblem(error: unknown): DashboardProblem | null {
  if (!error) {
    return null;
  }

  if (!(error instanceof ApiError)) {
    return "error";
  }

  const message = error.message.toLowerCase();
  if (message.includes("suspended") || message.includes("not active")) {
    return "suspended";
  }

  if (message.includes("business workspace")) {
    return "unavailable";
  }

  if (error.status === 403) {
    return "unauthorized";
  }

  return "error";
}
