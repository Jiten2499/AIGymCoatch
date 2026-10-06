"use client";

import { useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import {
  analyticsIsEmpty,
  customRange,
  dashboardAnalyticsQueryKey,
  formatAnalyticsLabel,
  getDashboardAnalytics,
  presetRange,
  tenantDate,
  type AnalyticsArea,
  type AnalyticsPreset,
  type AnalyticsReport,
  type DashboardRange,
} from "../lib/dashboardAnalytics";
import type { DashboardCapability } from "../lib/businessDashboard";

const charts: { area: AnalyticsArea; capability: string; title: string; empty: string }[] = [
  { area: "members", capability: "Members", title: "Member growth", empty: "Not enough member activity yet." },
  { area: "attendance", capability: "Attendance", title: "Attendance", empty: "No attendance data for this period." },
  { area: "memberships", capability: "Memberships", title: "Memberships", empty: "No membership activity for this period." },
];

const presets: { id: AnalyticsPreset; label: string }[] = [
  { id: "last7", label: "Last 7 days" },
  { id: "last30", label: "Last 30 days" },
  { id: "last90", label: "Last 90 days" },
  { id: "thisMonth", label: "This month" },
  { id: "custom", label: "Custom" },
];

export function DashboardAnalytics({ tenantId, currentDate, capabilities, onRange }: { tenantId: string | null; currentDate: string; capabilities: DashboardCapability[]; onRange?: (range: DashboardRange | null) => void }) {
  const [preset, setPreset] = useState<AnalyticsPreset>("last30");
  const [customFrom, setCustomFrom] = useState("");
  const [customTo, setCustomTo] = useState("");
  const today = tenantDate(currentDate);
  const range = useMemo(() => (preset === "custom" ? customRange(customFrom, customTo) : presetRange(preset, today)), [preset, customFrom, customTo, today]);
  const rangeKey = range ? `${range.from}|${range.to}|${range.period}` : "";
  useEffect(() => {
    onRange?.(range);
  }, [onRange, range, rangeKey]);
  const visible = charts.filter((chart) => capabilities.some((capability) => capability.code === chart.capability && capability.enabled));

  return (
    <section aria-label="Analytics" className="grid gap-4">
      <div className="flex flex-wrap gap-2">
        {presets.map((item) => (
          <button className={`min-h-11 rounded-full border px-4 text-sm font-semibold ${item.id === preset ? "border-accent bg-accent text-background" : "border-line bg-surface"}`} key={item.id} onClick={() => setPreset(item.id)} type="button">
            {item.label}
          </button>
        ))}
      </div>
      {preset === "custom" ? (
        <div className="flex flex-wrap gap-2">
          <input aria-label="From" className="field" onChange={(event) => setCustomFrom(event.target.value)} placeholder="YYYY-MM-DD" value={customFrom} />
          <input aria-label="To" className="field" onChange={(event) => setCustomTo(event.target.value)} placeholder="YYYY-MM-DD" value={customTo} />
        </div>
      ) : null}
      {!range && preset === "custom" ? <p className="text-muted">Choose a shorter range. Daily detail is limited to a month, weekly to two years, and monthly to three years.</p> : null}
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        {visible.map((chart) => (
          <AnalyticsCard chart={chart} key={chart.area} range={range} tenantId={tenantId} />
        ))}
      </div>
    </section>
  );
}

function AnalyticsCard({
  tenantId,
  chart,
  range,
}: {
  tenantId: string | null;
  chart: (typeof charts)[number];
  range: ReturnType<typeof customRange>;
}) {
  const query = useQuery({
    queryKey: dashboardAnalyticsQueryKey(tenantId, chart.area, range?.from ?? "", range?.to ?? "", range?.period ?? "Day"),
    enabled: Boolean(tenantId && range),
    placeholderData: (previousData, previousQuery) => (previousQuery?.queryKey[1] === tenantId ? previousData : undefined),
    queryFn: () => getDashboardAnalytics(chart.area, range!.from, range!.to, range!.period),
  });

  return (
    <article className="rounded-2xl border border-line bg-surface p-5">
      <h2 className="text-lg font-semibold">{chart.title}</h2>
      <ChartBody chart={chart} query={query} />
    </article>
  );
}

function ChartBody({ chart, query }: { chart: (typeof charts)[number]; query: { isError: boolean; isLoading: boolean; isFetching: boolean; data?: AnalyticsReport; refetch: () => Promise<unknown> } }) {
  if (query.isError) {
    return (
      <div className="mt-4">
        <p className="text-muted">Unable to load {chart.title.toLowerCase()}.</p>
        <button className="mt-3 min-h-11 rounded-full border border-line px-4" onClick={() => void query.refetch()} type="button">Try again</button>
      </div>
    );
  }
  if (query.isLoading || (query.isFetching && !query.data)) {
    return <p className="mt-4 text-muted" role="status">Loading {chart.title.toLowerCase()}.</p>;
  }
  if (query.data && analyticsIsEmpty(query.data)) {
    return <p className="mt-4 text-muted">{chart.empty}</p>;
  }
  if (query.data) {
    return <SeriesChart report={query.data} />;
  }
  return null;
}

function countLabel(value: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(value);
}

function labelStep(count: number): number {
  if (count <= 8) {
    return 1;
  }
  if (count <= 16) {
    return 2;
  }
  return Math.ceil(count / 6);
}

function SeriesChart({ report }: { report: AnalyticsReport }) {
  const [selected, setSelected] = useState(Math.max(0, (report.series[0]?.points.length ?? 1) - 1));
  const max = Math.max(1, ...report.series.flatMap((series) => series.points.map((point) => point.value)));
  const colors = ["var(--accent)", "var(--foreground)", "var(--muted)"];
  const point = report.series[0]?.points[selected];

  return (
    <div className="mt-4">
      {report.series.length > 1 ? (
        <div className="mb-3 flex flex-wrap gap-4 text-sm">
          {report.series.map((series) => (
            <span key={series.key}>{series.label}</span>
          ))}
        </div>
      ) : null}
      <div className="flex items-end gap-1 overflow-x-auto" style={{ minHeight: 160 }}>
        {report.series[0]?.points.map((item, index) => (
          <button
            aria-label={report.series.map((series) => `${formatAnalyticsLabel(series.points[index].periodStart, series.points[index].periodEnd, report.period)} ${series.label} ${countLabel(series.points[index].value)}`).join(", ")}
            className="flex min-h-11 min-w-7 flex-1 flex-col items-center justify-end"
            key={item.periodStart}
            onClick={() => setSelected(index)}
            type="button"
          >
            <span className="flex h-28 items-end gap-0.5">
              {report.series.map((series, seriesIndex) => {
                const value = series.points[index]?.value ?? 0;
                return <span key={series.key} className="block w-2 rounded-sm" style={{ background: colors[seriesIndex % colors.length], height: Math.max(value === 0 ? 2 : 8, Math.round((value / max) * 112)), opacity: value === 0 ? 0.35 : 1 }} />;
              })}
            </span>
            <span className="mt-1 h-4 text-[10px] text-muted">{index % labelStep(report.series[0]?.points.length ?? 0) === 0 ? formatAnalyticsLabel(item.periodStart, item.periodEnd, report.period) : ""}</span>
          </button>
        ))}
      </div>
      <p className="mt-3 text-sm">
        {point ? formatAnalyticsLabel(point.periodStart, point.periodEnd, report.period) : ""}. {report.series.map((series) => `${series.label}: ${countLabel(series.points[selected]?.value ?? 0)}`).join(" · ")}
      </p>
    </div>
  );
}
