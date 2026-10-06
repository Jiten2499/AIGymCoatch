"use client";

import { useQuery } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { api, ApiError } from "../../lib/api";
import { BusinessInsights } from "../../components/BusinessInsights";
import { DashboardAnalytics } from "../../components/DashboardAnalytics";
import { dashboardProblem, summaryHighlights, visibleWidgets, widgetMetrics, type BusinessDashboard, type MetricLine } from "../../lib/businessDashboard";
import type { DashboardRange } from "../../lib/dashboardAnalytics";
import { getSession, subscribeSession } from "../../lib/session";

export default function BusinessDashboardPage() {
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  const [analyticsRange, setAnalyticsRange] = useState<DashboardRange | null>(null);
  const dashboard = useQuery({
    queryKey: ["business-dashboard", tenantId],
    queryFn: () => api<BusinessDashboard>("/business/dashboard"),
  });
  const problem = dashboardProblem(dashboard.error);

  if (dashboard.isLoading) {
    return <p role="status">Loading the business dashboard.</p>;
  }

  if (problem === "suspended") {
    return <State title="This workspace is unavailable" body={message(dashboard.error)} />;
  }

  if (problem === "unauthorized") {
    return <State title="You cannot open this dashboard" body={message(dashboard.error)} />;
  }

  if (problem === "unavailable") {
    return <State title="No business workspace is active" body={message(dashboard.error)} />;
  }

  if (problem === "error") {
    return (
      <State title="The dashboard could not be loaded" body={message(dashboard.error)}>
        <button className="mt-4 min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => void dashboard.refetch()} type="button">
          Try again
        </button>
      </State>
    );
  }

  const data = dashboard.data;
  if (!data) {
    return <State title="Nothing to show yet" body="The workspace has not returned a dashboard." />;
  }

  const widgets = visibleWidgets(data.capabilities);
  const date = new Date(data.tenant.currentDate);

  return (
    <div className="grid gap-6">
      <header className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-4">
          {data.tenant.logoUrl ? <span aria-hidden className="h-12 w-12 shrink-0 rounded-xl bg-cover bg-center" style={{ backgroundImage: `url(${JSON.stringify(data.tenant.logoUrl)})` }} /> : null}
          <div>
            <h1 className="text-3xl font-semibold">{data.tenant.name}</h1>
            <p className="mt-1 text-muted">{data.user.displayName} · {data.user.roles.join(", ") || "Member"}</p>
          </div>
        </div>
        <p className="text-sm text-muted">
          {Number.isNaN(date.getTime()) ? data.tenant.currentDate : date.toLocaleString()} · {data.tenant.timeZone}
        </p>
      </header>
      <section aria-label="Summary" className="rounded-2xl border border-line bg-surface p-5">
        <h2 className="text-lg font-semibold">Summary</h2>
        <MetricList empty="No dashboard metrics are available for this workspace yet." lines={summaryHighlights(data.summary)} />
      </section>
      <DashboardAnalytics capabilities={data.capabilities} currentDate={data.tenant.currentDate} onRange={setAnalyticsRange} tenantId={data.tenant.id} />
      <BusinessInsights range={analyticsRange} tenantId={data.tenant.id} />
      <section aria-label="Dashboard areas">
        <h2 className="text-lg font-semibold">Areas</h2>
        {widgets.length === 0 ? (
          <p className="mt-3 text-muted">No dashboard areas are available for this workspace yet.</p>
        ) : (
          <div className="mt-3 grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
            {widgets.map((widget) => (
              <article className="rounded-2xl border border-line bg-surface p-5" key={widget.code}>
                <h3 className="font-semibold">{widget.title}</h3>
                <WidgetBody code={widget.code} fallback={widget.body} summary={data.summary} />
              </article>
            ))}
          </div>
        )}
      </section>
      <section aria-label="Recent activity" className="rounded-2xl border border-line bg-surface p-5">
        <h2 className="text-lg font-semibold">Recent activity</h2>
        <p className="mt-2 text-muted">Recent activity will appear here.</p>
      </section>
    </div>
  );
}

function WidgetBody({ code, summary, fallback }: { code: string; summary: BusinessDashboard["summary"]; fallback: string }) {
  const metrics = widgetMetrics(code, summary);
  if (metrics === "placeholder") {
    return <p className="mt-2 text-muted">{fallback}</p>;
  }

  if (metrics === "unavailable") {
    return <p className="mt-2 text-muted">This metric is not available for this workspace.</p>;
  }

  return <MetricList lines={metrics} />;
}

function MetricList({ lines, empty }: { lines: MetricLine[]; empty?: string }) {
  if (lines.length === 0) {
    return <p className="mt-2 text-muted">{empty}</p>;
  }

  return (
    <dl className="mt-3 grid gap-3 sm:grid-cols-2">
      {lines.map((line) => (
        <div key={line.label}>
          <dt className="text-sm text-muted">{line.label}</dt>
          <dd className="text-2xl font-semibold">{line.value}</dd>
        </div>
      ))}
    </dl>
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
