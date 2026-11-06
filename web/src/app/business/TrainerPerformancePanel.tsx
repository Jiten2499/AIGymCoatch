"use client";

import { useQuery } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { DataTable } from "../../components/DataTable";
import { PageState } from "../../components/AdminShell";
import { api, ApiError } from "../../lib/api";
import { monthBounds, quarterBounds } from "../../lib/trainerPeriod";
import { getSession, subscribeSession } from "../../lib/session";

type Row = { trainerId: string; name: string; ptRevenue: number | null; ptSessions: number | null; newPtClients: number; targetAchievementPercent: number | null };

export function TrainerPerformancePanel() {
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  const [period, setPeriod] = useState("Monthly");
  const [month, setMonth] = useState(new Date().toISOString().slice(0, 7));
  const [customFrom, setCustomFrom] = useState(monthBounds(month).from);
  const [customTo, setCustomTo] = useState(monthBounds(month).to);
  const range = period === "Quarterly" ? quarterBounds(month) : period === "Custom" ? { from: customFrom, to: customTo } : monthBounds(month);
  const report = useQuery({
    queryKey: ["trainer-performance", tenantId, range.from, range.to],
    queryFn: () => api<Row[]>(`/trainers/performance?from=${range.from}&to=${range.to}`),
  });
  const denied = report.error instanceof ApiError && report.error.status === 403;
  return (
    <PageState error={denied ? "You do not have permission to view trainer performance." : report.error instanceof ApiError ? report.error.message : null} loading={report.isLoading} onRetry={() => void report.refetch()}>
      <h1 className="text-3xl font-semibold">Trainer Performance</h1>
      <div className="mt-4 flex flex-wrap gap-2">
        <select aria-label="Period" className="field max-w-xs" onChange={(event) => setPeriod(event.target.value)} value={period}>
          <option>Monthly</option>
          <option>Quarterly</option>
          <option>Custom</option>
        </select>
        {period === "Custom" ? (
          <>
            <input aria-label="From" className="field max-w-xs" onChange={(event) => setCustomFrom(event.target.value)} type="date" value={customFrom} />
            <input aria-label="To" className="field max-w-xs" onChange={(event) => setCustomTo(event.target.value)} type="date" value={customTo} />
          </>
        ) : <input aria-label="Month" className="field max-w-xs" onChange={(event) => setMonth(event.target.value)} type="month" value={month} />}
      </div>
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Trainer", cell: (row) => row.name },
            { header: "PT Revenue", cell: (row) => row.ptRevenue === null ? "Unavailable" : String(row.ptRevenue) },
            { header: "PT Sessions", cell: (row) => row.ptSessions === null ? "Unavailable" : String(row.ptSessions) },
            { header: "New PT Clients", cell: (row) => String(row.newPtClients) },
            { header: "Target Achievement %", cell: (row) => row.targetAchievementPercent === null ? "—" : String(row.targetAchievementPercent) },
          ]}
          empty="No trainer performance is available for this period."
          rowKey={(row) => row.trainerId}
          rows={report.data ?? []}
        />
      </div>
    </PageState>
  );
}
