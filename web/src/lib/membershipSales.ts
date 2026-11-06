import { api } from "./api";
import { presetRange, tenantDate, type AnalyticsPeriod, type DashboardRange } from "./dashboardAnalytics";

export type MembershipSalesSlice = {
  planId: string;
  packageName: string;
  sold: number;
  percentage: number;
};

export type MembershipSalesReport = {
  from: string;
  to: string;
  totalSold: number;
  slices: MembershipSalesSlice[];
};

export function membershipSalesPath(from: string, to: string, period: AnalyticsPeriod): string {
  const query = new URLSearchParams({ from, to, period });
  return `/business/dashboard/analytics/membership-sales?${query.toString()}`;
}

export function getMembershipSales(from: string, to: string, period: AnalyticsPeriod): Promise<MembershipSalesReport> {
  return api<MembershipSalesReport>(membershipSalesPath(from, to, period));
}

export function salesRange(range: DashboardRange | null, currentDate: string): DashboardRange {
  return range ?? presetRange("thisMonth", tenantDate(currentDate));
}

export function formatSalesPercent(value: number): string {
  return `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(value)}%`;
}

export function salesTooltip(slice: MembershipSalesSlice): string {
  const sold = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(slice.sold);
  return `${slice.packageName}: ${formatSalesPercent(slice.percentage)} · ${sold} sold`;
}

export function salesPeriodLabel(from: string, to: string): string {
  const start = utcDate(from);
  const end = utcDate(to);
  const format = new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", year: "numeric", timeZone: "UTC" });
  return `${format.format(start)} – ${format.format(end)}`;
}

function utcDate(isoDate: string): Date {
  const [year, month, day] = isoDate.slice(0, 10).split("-").map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}
