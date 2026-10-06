"use client";

import { useQuery } from "@tanstack/react-query";
import { useSyncExternalStore } from "react";
import { getSession, subscribeSession } from "./session";
import { dashboardAnalyticsQueryKey, getDashboardAnalytics, type AnalyticsArea, type AnalyticsPeriod } from "./dashboardAnalytics";

export function useDashboardAnalytics(area: AnalyticsArea, from: string, to: string, period: AnalyticsPeriod) {
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  return useQuery({
    queryKey: dashboardAnalyticsQueryKey(tenantId, area, from, to, period),
    enabled: Boolean(tenantId && from && to),
    queryFn: () => getDashboardAnalytics(area, from, to, period),
  });
}
