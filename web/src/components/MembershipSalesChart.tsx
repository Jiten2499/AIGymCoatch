"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { ApiError } from "../lib/api";
import type { DashboardRange } from "../lib/dashboardAnalytics";
import {
  formatSalesPercent,
  getMembershipSales,
  salesPeriodLabel,
  salesRange,
  salesTooltip,
  type MembershipSalesSlice,
} from "../lib/membershipSales";

const colors = ["#c8f54e", "#7ec8ff", "#f0b429", "#f08a6a", "#c4b5fd", "#5eead4", "#fb7185", "#e8e2b0"];

export function MembershipSalesChart({ tenantId, currentDate, range }: { tenantId: string | null; currentDate: string; range: DashboardRange | null }) {
  const active = range ?? salesRange(null, currentDate);
  const query = useQuery({
    queryKey: ["membership-sales", tenantId, active.from, active.to, active.period],
    enabled: Boolean(tenantId && range),
    placeholderData: (previous, previousQuery) => (previousQuery?.queryKey[1] === tenantId ? previous : undefined),
    queryFn: () => getMembershipSales(active.from, active.to, active.period),
  });
  const [activeId, setActiveId] = useState<string | null>(null);

  return (
    <section aria-label="Membership sales" className="rounded-2xl border border-line bg-surface p-5">
      <h2 className="text-lg font-semibold">Membership Sales Distribution</h2>
      <p className="mt-1 text-sm text-muted">{range ? salesPeriodLabel(range.from, range.to) : "Select a period"}</p>
      {!range ? <p className="mt-4 text-muted">Choose a date range to see membership sales.</p> : null}
      {query.isError ? (
        <div className="mt-4">
          <p className="text-muted">{query.error instanceof ApiError ? query.error.message : "Unable to load membership sales."}</p>
          <button className="mt-3 min-h-11 rounded-full border border-line px-4" onClick={() => void query.refetch()} type="button">Try again</button>
        </div>
      ) : null}
      {!active ? <p className="mt-4 text-muted">Choose a date range to see membership sales.</p> : null}
      {active && (query.isLoading || (query.isFetching && !query.data)) && !query.isError ? <p className="mt-4 text-muted" role="status">Loading membership sales.</p> : null}
      {query.data && query.data.totalSold === 0 ? <p className="mt-4 text-muted">No memberships were sold in this period.</p> : null}
      {query.data && query.data.totalSold > 0 ? <SalesPie activeId={activeId} onActive={setActiveId} slices={query.data.slices} total={query.data.totalSold} /> : null}
    </section>
  );
}

function SalesPie({ slices, total, activeId, onActive }: { slices: MembershipSalesSlice[]; total: number; activeId: string | null; onActive: (planId: string | null) => void }) {
  const selected = slices.find((slice) => slice.planId === activeId) ?? null;
  const arcs = slices.map((slice, index) => {
    const start = slices.slice(0, index).reduce((sum, item) => sum + item.percentage, 0) / 100;
    return { slice, start, end: start + slice.percentage / 100 };
  });
  return (
    <div className="mt-4 grid gap-4 sm:grid-cols-[11rem_1fr] sm:items-center" onMouseLeave={() => onActive(null)}>
      <svg aria-label="Membership sales distribution" className="mx-auto" height="176" viewBox="0 0 176 176" width="176">
        {arcs.map(({ slice, start, end }, index) => {
          const label = salesTooltip(slice);
          const shared = {
            fill: colors[index % colors.length],
            opacity: selected && selected.planId !== slice.planId ? 0.72 : 1,
            role: "button" as const,
            tabIndex: 0,
            "aria-label": label,
            onMouseEnter: () => onActive(slice.planId),
            onClick: () => onActive(slice.planId),
            onFocus: () => onActive(slice.planId),
          };
          return slice.percentage >= 99.9 ? (
            <circle cx="88" cy="88" key={slice.planId} r="78" {...shared}><title>{label}</title></circle>
          ) : (
            <path d={wedge(88, 88, 78, start, end)} key={slice.planId} stroke="var(--surface)" strokeWidth="2" {...shared}><title>{label}</title></path>
          );
        })}
      </svg>
      <div>
        <p className="text-sm text-muted">Total memberships sold</p>
        <p className="text-2xl font-semibold">{new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(total)}</p>
        <ul className="mt-3 grid gap-1">
          {slices.map((slice, index) => (
            <li key={slice.planId}>
              <button className="flex min-h-11 w-full items-center gap-2 rounded-xl px-2 text-left text-sm" onClick={() => onActive(slice.planId)} onMouseEnter={() => onActive(slice.planId)} type="button">
                <span aria-hidden className="h-3 w-3 shrink-0 rounded-full" style={{ background: colors[index % colors.length] }} />
                <span className="min-w-0 flex-1 truncate">{slice.packageName}</span>
                <span className="text-muted">{formatSalesPercent(slice.percentage)}</span>
              </button>
            </li>
          ))}
        </ul>
        <p className="mt-3 rounded-xl border border-line bg-background px-3 py-2 text-sm" role="status">
          {selected ? salesTooltip(selected) : "Hover or tap a package to see its share."}
        </p>
      </div>
    </div>
  );
}

function wedge(cx: number, cy: number, radius: number, start: number, end: number): string {
  const point = (turn: number) => {
    const angle = turn * Math.PI * 2 - Math.PI / 2;
    return [cx + radius * Math.cos(angle), cy + radius * Math.sin(angle)];
  };
  const [x0, y0] = point(start);
  const [x1, y1] = point(end);
  const large = end - start > 0.5 ? 1 : 0;
  return `M ${cx} ${cy} L ${x0} ${y0} A ${radius} ${radius} 0 ${large} 1 ${x1} ${y1} Z`;
}
