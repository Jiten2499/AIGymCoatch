"use client";

import type { AnalyticsPreset } from "../../../lib/dashboardAnalytics";
import { formatMoney } from "../../../lib/dashboardBoard";

export function RangeFilter(props: {
  preset: AnalyticsPreset;
  from: string;
  to: string;
  invalid: boolean;
  onPreset: (preset: AnalyticsPreset) => void;
  onCustom: (from: string, to: string) => void;
}) {
  const presets: Array<[AnalyticsPreset, string]> = [
    ["thisMonth", "This month"],
    ["last7", "7 days"],
    ["last30", "30 days"],
    ["last90", "90 days"],
    ["custom", "Custom"],
  ];
  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap gap-2">
        {presets.map(([value, label]) => (
          <button
            key={value}
            type="button"
            onClick={() => props.onPreset(value)}
            className={`min-h-11 rounded-full border px-4 text-sm font-semibold ${props.preset === value ? "border-accent bg-accent text-background" : "border-line bg-surface"}`}
          >
            {label}
          </button>
        ))}
      </div>
      {props.preset === "custom" ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted">
          <input aria-label="From" className="field" onChange={(event) => props.onCustom(event.target.value, props.to)} type="date" value={props.from} />
          <input aria-label="To" className="field" onChange={(event) => props.onCustom(props.from, event.target.value)} type="date" value={props.to} />
        </div>
      ) : null}
      {props.invalid ? <p className="text-sm text-muted">Choose a shorter range. Daily detail is limited to about a month.</p> : null}
    </div>
  );
}

export function Skeleton() {
  return (
    <div className="mt-3 grid gap-2">
      <span className="h-12 animate-pulse rounded-xl bg-background" />
      <span className="h-12 animate-pulse rounded-xl bg-background" />
    </div>
  );
}

export function count(value: number | null | undefined): string {
  return value == null ? "—" : new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(value);
}

export function moneyOrDash(value: number | null | undefined, currency: string): string {
  return value == null ? "—" : formatMoney(value, currency);
}

export function timeLabel(utc: string): string {
  const parsed = new Date(utc);
  return Number.isNaN(parsed.getTime()) ? utc : parsed.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
}
