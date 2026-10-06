"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { dashboardInsightsQueryKey, getDashboardInsights, sameTenantInsight, type BusinessDashboardInsights } from "../lib/dashboardInsights";
import type { DashboardRange } from "../lib/dashboardAnalytics";

export function BusinessInsights({ tenantId, range }: { tenantId: string | null; range: DashboardRange | null }) {
  const [refresh, setRefresh] = useState(0);
  const query = useQuery({
    queryKey: dashboardInsightsQueryKey(tenantId, range?.from ?? "", range?.to ?? "", range?.period ?? "Day", refresh),
    enabled: Boolean(tenantId && range),
    placeholderData: (previous, previousQuery) => sameTenantInsight(previousQuery?.queryKey[1], tenantId, previous),
    queryFn: () => getDashboardInsights(range!.from, range!.to, range!.period, refresh > 0),
  });

  if (!range) {
    return null;
  }

  const loading = query.isLoading || (query.isFetching && !query.data);
  return (
    <div className="grid gap-4 xl:grid-cols-2">
      <Attention report={query.data} failed={query.isError} loading={loading} onRetry={() => void query.refetch()} />
      <AiInsights report={query.data} failed={query.isError} loading={loading} onRefresh={() => setRefresh((value) => value + 1)} onRetry={() => void query.refetch()} />
    </div>
  );
}

function Attention({ report, failed, loading, onRetry }: { report?: BusinessDashboardInsights; failed: boolean; loading: boolean; onRetry: () => void }) {
  return (
    <article aria-label="Attention needed" className="rounded-2xl border border-line bg-surface p-5">
      <h2 className="text-lg font-semibold">Attention needed</h2>
      {loading ? <p className="mt-4 text-muted" role="status">Checking operational signals...</p> : null}
      {failed ? <Failure message="Attention items are temporarily unavailable." onRetry={onRetry} /> : null}
      {!loading && !failed && (report?.attention.length ?? 0) === 0 ? <p className="mt-4 text-muted">No attention items for this period.</p> : null}
      {!loading && !failed ? report?.attention.map((item) => (
        <div className="mt-4" key={`${item.type}-${item.title}`}>
          <p className="text-sm font-semibold">{item.priority} · {item.title}</p>
          <p className="mt-1">{item.description}</p>
        </div>
      )) : null}
    </article>
  );
}

function AiInsights({ report, failed, loading, onRefresh, onRetry }: { report?: BusinessDashboardInsights; failed: boolean; loading: boolean; onRefresh: () => void; onRetry: () => void }) {
  const ai = report?.ai;
  return (
    <article aria-label="AI business insights" className="rounded-2xl border border-line bg-surface p-5">
      <h2 className="text-lg font-semibold">AI business insights</h2>
      {loading ? <p className="mt-4 text-muted" role="status">Analyzing your business data...</p> : null}
      {failed || ai?.status === "Unavailable" ? <Failure message="AI insights are temporarily unavailable." onRetry={onRetry} /> : null}
      {!failed && ai?.status === "Disabled" ? <p className="mt-4 text-muted">AI insights are not included in your current plan.</p> : null}
      {!failed && ai?.status === "Empty" ? <p className="mt-4 text-muted">Not enough business activity to generate meaningful AI insights yet.</p> : null}
      {!failed && ai?.status === "Ready" ? (
        <div className="mt-4 grid gap-4">
          {ai.summary ? <p>{ai.summary}</p> : null}
          {ai.insights.map((item) => (
            <div key={`${item.type}-${item.title}`}>
              <p className="text-sm font-semibold">{item.type} · {item.title}</p>
              <p className="mt-1">{item.message}</p>
            </div>
          ))}
          {ai.recommendations.map((item) => (
            <div key={item.title}>
              <p className="text-sm font-semibold">Recommendation · {item.title}</p>
              <p className="mt-1">{item.description}</p>
            </div>
          ))}
          <button className="min-h-11 w-fit rounded-full border border-line px-4" onClick={onRefresh} type="button">Refresh insights</button>
        </div>
      ) : null}
    </article>
  );
}

function Failure({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="mt-4">
      <p className="text-muted">{message}</p>
      <button className="mt-3 min-h-11 rounded-full border border-line px-4" onClick={onRetry} type="button">Try again</button>
    </div>
  );
}
