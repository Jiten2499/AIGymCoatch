import { api } from "./api";

export type AnalyticsPeriod = "Day" | "Week" | "Month";

export type AnalyticsArea = "members" | "attendance" | "memberships";

export type AnalyticsPoint = {
  periodStart: string;
  periodEnd: string;
  label: string;
  value: number;
};

export type AnalyticsSeries = {
  key: string;
  label: string;
  points: AnalyticsPoint[];
};

export type AnalyticsReport = {
  from: string;
  to: string;
  period: AnalyticsPeriod;
  series: AnalyticsSeries[];
};

export function dashboardAnalyticsQueryKey(tenantId: string | null | undefined, area: AnalyticsArea, from: string, to: string, period: AnalyticsPeriod) {
  return ["business-dashboard-analytics", tenantId ?? null, area, from, to, period] as const;
}

export function analyticsPath(area: AnalyticsArea, from: string, to: string, period: AnalyticsPeriod): string {
  const query = new URLSearchParams({ from, to, period });
  return `/business/dashboard/analytics/${area}?${query.toString()}`;
}

export function getDashboardAnalytics(area: AnalyticsArea, from: string, to: string, period: AnalyticsPeriod): Promise<AnalyticsReport> {
  return api<AnalyticsReport>(analyticsPath(area, from, to, period));
}

export type AnalyticsPreset = "last7" | "last30" | "last90" | "thisMonth" | "custom";

export type DashboardRange = {
  from: string;
  to: string;
  period: AnalyticsPeriod;
};

const maxMonthlyDays = 36 * 31;
const maxWeeklyDays = 104 * 7;

export function tenantDate(currentDate: string): string {
  return currentDate.slice(0, 10);
}

export function addDays(isoDate: string, days: number): string {
  const [year, month, day] = isoDate.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

export function inclusiveDays(from: string, to: string): number {
  const start = Date.parse(`${from}T00:00:00Z`);
  const end = Date.parse(`${to}T00:00:00Z`);
  if (Number.isNaN(start) || Number.isNaN(end)) {
    return 0;
  }
  return Math.round((end - start) / 86_400_000) + 1;
}

export function periodForRange(from: string, to: string): AnalyticsPeriod | null {
  const days = inclusiveDays(from, to);
  if (days < 1) {
    return null;
  }
  if (days <= 31) {
    return "Day";
  }
  if (days <= maxWeeklyDays) {
    return "Week";
  }
  if (days <= maxMonthlyDays) {
    return "Month";
  }
  return null;
}

export function presetRange(preset: Exclude<AnalyticsPreset, "custom">, today: string): DashboardRange {
  if (preset === "last7") {
    return { from: addDays(today, -6), to: today, period: "Day" };
  }
  if (preset === "last30") {
    return { from: addDays(today, -29), to: today, period: "Day" };
  }
  if (preset === "last90") {
    return { from: addDays(today, -89), to: today, period: "Week" };
  }
  return { from: `${today.slice(0, 8)}01`, to: today, period: "Day" };
}

export function customRange(from: string, to: string): DashboardRange | null {
  const period = periodForRange(from, to);
  return period ? { from, to, period } : null;
}

export function analyticsIsEmpty(report: AnalyticsReport | undefined): boolean {
  if (!report || report.series.length === 0) {
    return true;
  }
  return report.series.every((series) => series.points.every((point) => point.value === 0));
}

export function formatAnalyticsLabel(periodStart: string, periodEnd: string, period: AnalyticsPeriod): string {
  const start = utcDate(periodStart);
  const end = utcDate(periodEnd);
  if (period === "Month") {
    return new Intl.DateTimeFormat("en", { month: "short", year: "numeric", timeZone: "UTC" }).format(start);
  }
  if (period === "Week") {
    const startLabel = new Intl.DateTimeFormat("en", { month: "short", day: "numeric", timeZone: "UTC" }).format(start);
    const endLabel = new Intl.DateTimeFormat("en", { month: "short", day: "numeric", timeZone: "UTC" }).format(end);
    return `${startLabel}–${endLabel}`;
  }
  return new Intl.DateTimeFormat("en", { month: "short", day: "2-digit", timeZone: "UTC" }).format(start);
}

function utcDate(isoDate: string): Date {
  const [year, month, day] = isoDate.split("-").map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}
