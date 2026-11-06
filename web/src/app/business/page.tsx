"use client";

import { useQuery } from "@tanstack/react-query";
import { useMemo, useState, useSyncExternalStore } from "react";
import { BusinessDashboardHome } from "./dashboard/BusinessDashboardHome";
import { RoleDashboard, type MyDashboard } from "./dashboard/RoleDashboard";
import { api, ApiError } from "../../lib/api";
import {
  customRange,
  presetRange,
  tenantDate,
  type AnalyticsPreset,
  type DashboardRange,
} from "../../lib/dashboardAnalytics";
import { dashboardGreeting, getDashboardBoard } from "../../lib/dashboardBoard";
import { dashboardProblem, type BusinessDashboard } from "../../lib/businessDashboard";
import { getSession, subscribeSession } from "../../lib/session";

export default function BusinessDashboardPage() {
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const userId = session?.user.id ?? null;
  const [preset, setPreset] = useState<AnalyticsPreset>("thisMonth");
  const [customFrom, setCustomFrom] = useState("");
  const [customTo, setCustomTo] = useState("");
  const mine = useQuery({
    queryKey: ["my-dashboard", tenantId, userId],
    enabled: tenantId !== null && userId !== null,
    queryFn: () => api<MyDashboard>("/business/dashboard/mine"),
  });
  const ownerLayout = mine.data?.layout === "owner";
  const dashboard = useQuery({
    queryKey: ["business-dashboard", tenantId],
    enabled: ownerLayout,
    queryFn: () => api<BusinessDashboard>("/business/dashboard"),
  });

  const today = dashboard.data ? tenantDate(dashboard.data.tenant.currentDate) : tenantDate(new Date().toISOString());
  const range = useMemo<DashboardRange | null>(
    () => (preset === "custom" ? customRange(customFrom, customTo) : presetRange(preset, today)),
    [preset, customFrom, customTo, today],
  );
  const activeRange = range ?? presetRange("thisMonth", today);

  const board = useQuery({
    queryKey: ["business-dashboard-board", tenantId, activeRange.from, activeRange.to, activeRange.period],
    enabled: ownerLayout && dashboard.isSuccess,
    placeholderData: (previous) => previous,
    queryFn: () => getDashboardBoard(activeRange.from, activeRange.to, activeRange.period),
  });

  const activeError = mine.isError ? mine.error : dashboard.error;
  const problem = dashboardProblem(activeError);

  if (tenantId === null || mine.isLoading || (ownerLayout && dashboard.isLoading)) {
    return <p role="status">Loading the business dashboard.</p>;
  }

  if (problem === "suspended") {
    return <State title="This workspace is unavailable" body={message(activeError)} />;
  }
  if (problem === "unauthorized") {
    return <State title="You cannot open this dashboard" body={message(activeError)} />;
  }
  if (problem === "unavailable") {
    return <State title="No business workspace is active" body={message(activeError)} />;
  }
  if (problem === "error") {
    return (
      <State title="The dashboard could not be loaded" body={message(activeError)}>
        <button className="mt-4 min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => void (mine.isError ? mine.refetch() : dashboard.refetch())} type="button">Try again</button>
      </State>
    );
  }
  if (mine.data && mine.data.layout !== "owner") {
    return <RoleDashboard dashboard={mine.data} />;
  }
  if (!dashboard.data) {
    return <State title="Nothing to show yet" body="The workspace has not returned a dashboard." />;
  }

  const data = dashboard.data;
  const date = new Date(data.tenant.currentDate);
  const membershipsEnabled = data.capabilities.some((capability) => capability.code === "Memberships" && capability.enabled);

  return (
    <BusinessDashboardHome
      greeting={dashboardGreeting(data.user.displayName, data.tenant.currentDate)}
      workspace={`${data.tenant.name} · ${data.tenant.timeZone}`}
      dateLabel={Number.isNaN(date.getTime()) ? data.tenant.currentDate : date.toLocaleDateString(undefined, { weekday: "long", year: "numeric", month: "long", day: "numeric" })}
      tenantId={data.tenant.id}
      currentDate={data.tenant.currentDate}
      membershipsEnabled={membershipsEnabled}
      range={activeRange}
      preset={preset}
      customFrom={customFrom}
      customTo={customTo}
      rangeInvalid={preset === "custom" && range === null}
      board={{
        data: board.data,
        isLoading: board.isLoading || (board.isFetching && !board.data),
        isError: board.isError,
        error: board.error instanceof ApiError ? board.error.message : board.isError ? "Unable to load dashboard metrics." : null,
        refetch: () => void board.refetch(),
      }}
      onPreset={setPreset}
      onCustom={(from, to) => { setCustomFrom(from); setCustomTo(to); }}
    />
  );
}

function State({ title, body, children }: { title: string; body: string; children?: React.ReactNode }) {
  return (
    <div className="rounded-2xl border border-line bg-surface p-6" role="alert">
      <h1 className="text-xl font-semibold">{title}</h1>
      <p className="mt-2 text-muted">{body}</p>
      {children}
    </div>
  );
}

function message(error: unknown): string {
  return error instanceof ApiError ? error.message : "The dashboard could not be loaded.";
}
