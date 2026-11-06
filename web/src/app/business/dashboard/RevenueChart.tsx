"use client";

import { useState } from "react";
import { formatMoney, revenueIsEmpty, type RevenuePoint } from "../../../lib/dashboardBoard";

export function RevenueChart({ points, currency }: { points: RevenuePoint[]; currency: string }) {
  const [active, setActive] = useState<number | null>(null);
  const max = Math.max(1, ...points.map((point) => point.amount));
  const step = labelStep(points.length);
  const selected = active !== null ? points[active] : null;
  const total = points.reduce((sum, point) => sum + point.amount, 0);

  return (
    <div className="mt-4">
      <div className="flex items-baseline justify-between gap-3">
        <p className="text-2xl font-semibold">{formatMoney(total, currency)}</p>
        <p className="min-h-5 text-sm text-muted" role="status">
          {selected ? `${selected.label}: ${formatMoney(selected.amount, currency)}` : "Hover or tap a bar for its total."}
        </p>
      </div>
      <div className="mt-3 flex items-end gap-1 overflow-x-auto" style={{ minHeight: 168 }} onMouseLeave={() => setActive(null)}>
        {points.map((point, index) => (
          <button
            aria-label={`${point.label}: ${formatMoney(point.amount, currency)}`}
            className="flex min-h-11 min-w-6 flex-1 flex-col items-center justify-end"
            key={point.periodStart}
            onClick={() => setActive(index)}
            onFocus={() => setActive(index)}
            onMouseEnter={() => setActive(index)}
            type="button"
          >
            <span className="flex h-32 w-full items-end justify-center">
              <span
                className="block w-3 rounded-t"
                style={{
                  background: "var(--accent)",
                  height: `${Math.max(point.amount === 0 ? 2 : 6, Math.round((point.amount / max) * 128))}px`,
                  opacity: point.amount === 0 ? 0.35 : active === null || active === index ? 1 : 0.5,
                }}
              />
            </span>
            <span className="mt-1 h-4 text-[10px] text-muted">{index % step === 0 ? point.label : ""}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

export function revenueEmpty(points: RevenuePoint[] | null | undefined): boolean {
  return revenueIsEmpty(points);
}

function labelStep(count: number): number {
  if (count <= 8) {
    return 1;
  }
  if (count <= 16) {
    return 2;
  }
  return Math.ceil(count / 8);
}
