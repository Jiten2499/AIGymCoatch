import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { apiClient } from '../../../services/api/client';
import { useSessionStore } from '../../../store/sessionStore';
import type { AnalyticsPeriod } from './analytics.api';

export type AttentionItem = {
  type: string;
  priority: 'Low' | 'Medium' | 'High' | string;
  title: string;
  description: string;
  value: number | null;
  action: string | null;
};

export type AiInsightItem = {
  type: string;
  title: string;
  message: string;
  priority: string;
};

export type AiRecommendation = {
  title: string;
  description: string;
};

export type AiInsightSection = {
  status: 'Ready' | 'Empty' | 'Unavailable' | 'Disabled' | string;
  summary: string | null;
  insights: AiInsightItem[];
  recommendations: AiRecommendation[];
};

export type BusinessDashboardInsights = {
  attention: AttentionItem[];
  ai: AiInsightSection;
};

export function dashboardInsightsQueryKey(tenantId: string | null | undefined, from: string, to: string, period: AnalyticsPeriod, refresh: number) {
  return ['business-dashboard-insights', tenantId ?? null, from, to, period, refresh] as const;
}

export function insightsPath(from: string, to: string, period: AnalyticsPeriod, refresh: boolean): string {
  const query = new URLSearchParams({ from, to, period, refresh: String(refresh) });
  return `/business/dashboard/insights?${query.toString()}`;
}

export async function getDashboardInsights(from: string, to: string, period: AnalyticsPeriod, refresh: boolean, signal?: AbortSignal): Promise<BusinessDashboardInsights> {
  const response = await apiClient.get<BusinessDashboardInsights>(insightsPath(from, to, period, refresh), { signal });
  return response.data;
}

export function useBusinessInsights(from: string, to: string, period: AnalyticsPeriod, enabled: boolean) {
  const tenantId = useSessionStore((state) => state.currentTenant?.id);
  const [refresh, setRefresh] = useState(0);
  const query = useQuery<BusinessDashboardInsights>({
    queryKey: dashboardInsightsQueryKey(tenantId, from, to, period, refresh),
    enabled: Boolean(enabled && tenantId && from && to),
    placeholderData: (previous, previousQuery) => (previousQuery?.queryKey[1] === tenantId ? previous : undefined),
    queryFn: ({ signal }) => getDashboardInsights(from, to, period, refresh > 0, signal),
  });
  return { query, refreshInsights: () => setRefresh((value) => value + 1) };
}
