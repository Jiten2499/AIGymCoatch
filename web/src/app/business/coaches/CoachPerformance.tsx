"use client";

import { useQueries } from "@tanstack/react-query";
import { useMemo } from "react";
import { api } from "../../../lib/api";
import { monthBounds } from "../../../lib/trainerPeriod";
import { CoachKpiCard, CoachMetricIcon } from "./CoachKpiCard";

type PerformancePoint = {
  month: string;
  label: string;
  newPtClients: number | null;
  ptSessionsCompleted: number | null;
  ptPackagesSold: number | null;
};

type CoachPerformanceProps = {
  coachId: string;
  month: string;
  newPtClients: number;
  ptSessionsCompleted?: number | null;
  ptPackagesSold?: number | null;
  activePtClients: number;
};

function priorMonths(month: string, count: number) {
  const [yearText, monthText] = month.split("-");
  let year = Number(yearText);
  let monthNumber = Number(monthText);
  const items: string[] = [];
  for (let index = 0; index < count; index += 1) {
    items.unshift(`${year}-${String(monthNumber).padStart(2, "0")}`);
    monthNumber -= 1;
    if (monthNumber < 1) {
      monthNumber = 12;
      year -= 1;
    }
  }
  return items;
}

function monthLabel(month: string) {
  const date = new Date(`${month}-01T00:00:00`);
  return Number.isNaN(date.getTime()) ? month : date.toLocaleString(undefined, { month: "short", year: "2-digit" });
}

function displayCount(value: number | null | undefined) {
  return value === null || value === undefined ? "—" : String(value);
}

export function CoachPerformance({
  coachId,
  month,
  newPtClients,
  ptSessionsCompleted,
  ptPackagesSold,
  activePtClients,
}: CoachPerformanceProps) {
  const months = useMemo(() => priorMonths(month, 6), [month]);
  const history = useQueries({
    queries: months.map((item) => {
      const range = monthBounds(item);
      return {
        queryKey: ["coach-workspace", coachId, range.from],
        queryFn: () => api<{
          newPtClients: number;
          ptSessionsCompleted?: number | null;
          ptPackagesSold?: number | null;
        }>(`/trainers/${coachId}/coach?from=${range.from}&to=${range.to}`),
        staleTime: 60_000,
      };
    }),
  });

  const points: PerformancePoint[] = months.map((item, index) => {
    const row = history[index]?.data;
    const loading = history[index]?.isLoading;
    return {
      month: item,
      label: monthLabel(item),
      newPtClients: loading ? null : row ? row.newPtClients : null,
      ptSessionsCompleted: loading ? null : row ? row.ptSessionsCompleted ?? null : null,
      ptPackagesSold: loading ? null : row ? row.ptPackagesSold ?? null : null,
    };
  });
  const loadingTrend = history.some((item) => item.isLoading);
  const hasTrendValues = points.some((point) => point.newPtClients !== null || point.ptSessionsCompleted !== null || point.ptPackagesSold !== null);

  const breakdown = [
    { label: "PT Sessions Completed", note: "Completed personal-training sessions in the selected period.", value: displayCount(ptSessionsCompleted) },
    { label: "New PT Clients", note: "Assignments that started in the selected period.", value: String(newPtClients) },
    { label: "PT Packages Sold", note: "Standalone package assignments recorded in the period.", value: displayCount(ptPackagesSold) },
    { label: "Active PT Clients", note: "Assignments still open at the end of the selected period.", value: String(activePtClients) },
    { label: "Eligible PT Revenue", note: "Requires qualifying personal-training payment records.", value: "Unavailable" },
  ];

  return (
    <div className="mt-4 grid gap-5">
      <div className="grid gap-3 sm:grid-cols-3">
        <CoachKpiCard icon={<CoachMetricIcon name="sessions" />} label="PT Sessions Completed" note="Selected period" value={displayCount(ptSessionsCompleted)} />
        <CoachKpiCard icon={<CoachMetricIcon name="new" />} label="New PT Clients" note="Assignment start dates" value={String(newPtClients)} />
        <CoachKpiCard icon={<CoachMetricIcon name="packages" />} label="PT Packages Sold" note="Standalone assignments" value={displayCount(ptPackagesSold)} />
      </div>

      <section className="rounded-2xl border border-line bg-background p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h3 className="text-base font-semibold">Performance Trends</h3>
            <p className="mt-1 text-sm text-muted">Last six months ending {monthLabel(month)}, using the same coach workspace totals.</p>
          </div>
          <div className="flex flex-wrap gap-3 text-xs text-muted">
            <Legend color="var(--color-accent, #b6ff3b)" label="New PT clients" />
            <Legend color="#7dd3fc" label="PT sessions" />
            <Legend color="#c4b5fd" label="Packages sold" />
          </div>
        </div>
        {loadingTrend && !hasTrendValues ? <p className="mt-6 text-sm text-muted" role="status">Loading performance trend…</p> : null}
        {!loadingTrend && !hasTrendValues ? (
          <div className="mt-6 rounded-xl border border-dashed border-line px-4 py-8 text-center text-sm text-muted">
            Not enough period data to chart performance yet.
          </div>
        ) : (
          <PerformanceTrendChart points={points} />
        )}
      </section>

      <section className="rounded-2xl border border-line bg-background p-5">
        <h3 className="text-base font-semibold">PT Revenue Summary</h3>
        <p className="mt-1 text-sm text-muted">Eligible personal-training revenue for commission calculations.</p>
        <div className="mt-4 rounded-xl border border-dashed border-line px-4 py-6">
          <p className="text-sm text-muted">Eligible PT revenue</p>
          <p className="mt-2 text-2xl font-semibold">Unavailable</p>
          <p className="mt-2 max-w-2xl text-sm text-muted">No qualifying personal-training payment records exist for this period, so revenue is not shown as zero.</p>
        </div>
      </section>

      <section className="rounded-2xl border border-line bg-background p-5">
        <h3 className="text-base font-semibold">Detailed Performance Breakdown</h3>
        <p className="mt-1 text-sm text-muted">Metric descriptions and values for the selected period.</p>
        <ul className="mt-4 grid gap-3">
          {breakdown.map((item) => (
            <li className="grid gap-1 rounded-xl border border-line px-4 py-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center" key={item.label}>
              <div>
                <p className="font-semibold">{item.label}</p>
                <p className="text-sm text-muted">{item.note}</p>
              </div>
              <p className="text-xl font-semibold tracking-tight sm:text-right">{item.value}</p>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

function Legend({ color, label }: { color: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-2">
      <span aria-hidden className="size-2.5 rounded-full" style={{ background: color }} />
      {label}
    </span>
  );
}

function PerformanceTrendChart({ points }: { points: PerformancePoint[] }) {
  const width = 640;
  const height = 220;
  const padX = 28;
  const padY = 24;
  const series = [
    { key: "newPtClients" as const, color: "var(--color-accent, #b6ff3b)" },
    { key: "ptSessionsCompleted" as const, color: "#7dd3fc" },
    { key: "ptPackagesSold" as const, color: "#c4b5fd" },
  ];
  const values = points.flatMap((point) => series.map((item) => point[item.key]).filter((value): value is number => value !== null));
  const max = Math.max(1, ...values);
  const step = points.length > 1 ? (width - padX * 2) / (points.length - 1) : 0;

  function y(value: number) {
    return height - padY - ((value / max) * (height - padY * 2));
  }

  function pathFor(key: keyof Omit<PerformancePoint, "month" | "label">) {
    const commands: string[] = [];
    points.forEach((point, index) => {
      const value = point[key];
      if (value === null) {
        return;
      }
      const x = padX + step * index;
      commands.push(`${commands.length === 0 ? "M" : "L"} ${x} ${y(value)}`);
    });
    return commands.join(" ");
  }

  return (
    <div className="mt-4 overflow-x-auto">
      <svg aria-label="Performance trends" className="min-w-full" height={height} role="img" viewBox={`0 0 ${width} ${height}`} width={width}>
        {[0, 0.5, 1].map((ratio) => {
          const value = Math.round(max * ratio);
          const lineY = y(value);
          return (
            <g key={ratio}>
              <line stroke="currentColor" strokeOpacity="0.12" x1={padX} x2={width - padX} y1={lineY} y2={lineY} />
              <text fill="currentColor" fontSize="10" opacity="0.55" x={4} y={lineY + 3}>{value}</text>
            </g>
          );
        })}
        {series.map((item) => {
          const d = pathFor(item.key);
          return d ? <path d={d} fill="none" key={item.key} stroke={item.color} strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" /> : null;
        })}
        {points.map((point, index) => (
          <text fill="currentColor" fontSize="11" key={point.month} opacity="0.7" textAnchor="middle" x={padX + step * index} y={height - 6}>{point.label}</text>
        ))}
      </svg>
    </div>
  );
}
