import { api } from "./api";
import type { AnalyticsPeriod } from "./dashboardAnalytics";

export type AttentionItem = {
  type: string;
  priority: string;
  title: string;
  description: string;
  value: number | null;
  action: string | null;
};

export type AiInsightSection = {
  status: string;
  summary: string | null;
  insights: { type: string; title: string; message: string; priority: string }[];
  recommendations: { title: string; description: string }[];
};

export type BusinessDashboardInsights = {
  attention: AttentionItem[];
  ai: AiInsightSection;
};

export function dashboardInsightsQueryKey(tenantId: string | null | undefined, from: string, to: string, period: AnalyticsPeriod, refresh: number) {
  return ["business-dashboard-insights", tenantId ?? null, from, to, period, refresh] as const;
}

export function insightsPath(from: string, to: string, period: AnalyticsPeriod, refresh: boolean): string {
  const query = new URLSearchParams({ from, to, period, refresh: String(refresh) });
  return `/business/dashboard/insights?${query.toString()}`;
}

export function getDashboardInsights(from: string, to: string, period: AnalyticsPeriod, refresh: boolean): Promise<BusinessDashboardInsights> {
  return api<BusinessDashboardInsights>(insightsPath(from, to, period, refresh));
}

export function sameTenantInsight(previousTenantId: unknown, tenantId: string | null, previous: BusinessDashboardInsights | undefined) {
  return previousTenantId === tenantId ? previous : undefined;
}
