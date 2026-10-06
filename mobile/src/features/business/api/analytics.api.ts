import { useQuery } from '@tanstack/react-query';
import { apiClient } from '../../../services/api/client';
import { useSessionStore } from '../../../store/sessionStore';

export type AnalyticsPeriod = 'Day' | 'Week' | 'Month';

export type AnalyticsArea = 'members' | 'attendance' | 'memberships';

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
  return ['business-dashboard-analytics', tenantId ?? null, area, from, to, period] as const;
}

export function analyticsPath(area: AnalyticsArea, from: string, to: string, period: AnalyticsPeriod): string {
  const query = new URLSearchParams({ from, to, period });
  return `/business/dashboard/analytics/${area}?${query.toString()}`;
}

export async function getDashboardAnalytics(area: AnalyticsArea, from: string, to: string, period: AnalyticsPeriod, signal?: AbortSignal): Promise<AnalyticsReport> {
  const response = await apiClient.get<AnalyticsReport>(analyticsPath(area, from, to, period), { signal });
  return response.data;
}

export function useDashboardAnalytics(area: AnalyticsArea, from: string, to: string, period: AnalyticsPeriod, enabled = true) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id);
  return useQuery<AnalyticsReport>({
    queryKey: dashboardAnalyticsQueryKey(tenantId, area, from, to, period),
    enabled: Boolean(enabled && tenantId && from && to),
    placeholderData: (previousData, previousQuery) => (previousQuery?.queryKey[1] === tenantId ? previousData : undefined),
    queryFn: ({ signal }) => getDashboardAnalytics(area, from, to, period, signal),
  });
}
