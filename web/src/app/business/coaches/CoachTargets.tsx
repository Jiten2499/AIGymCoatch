"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { ScrollableGrid } from "../../../components/ScrollableGrid";
import { api, ApiError } from "../../../lib/api";

export type CoachTarget = {
  id: string;
  name: string | null;
  targetType: string;
  period: string;
  periodStart: string;
  periodEnd: string;
  targetValue: number;
  actual: number | null;
  achievementPercent: number | null;
  isActive: boolean;
};

type CoachTargetsProps = {
  coachId: string;
  targets: CoachTarget[];
  range: { from: string; to: string };
  onChanged: () => void;
};

const targetLabels: Record<string, string> = { NewPtClients: "New PT clients", PtRevenue: "PT revenue", PtSessions: "PT sessions" };

export function CoachMonthlyTargets({ coachId, targets, range, onChanged }: CoachTargetsProps) {
  const [name, setName] = useState("New PT clients");
  const [targetType, setTargetType] = useState("NewPtClients");
  const [targetValue, setTargetValue] = useState("1");
  const save = useMutation({
    mutationFn: () => api(`/trainers/${coachId}/targets`, { method: "POST", body: JSON.stringify({ name, targetType, period: "Monthly", start: range.from, end: range.to, targetValue: Number(targetValue) }) }),
    onSuccess: onChanged,
  });
  const toggle = useMutation({
    mutationFn: (target: CoachTarget) => api(`/trainers/targets/${target.id}/active`, { method: "POST", body: JSON.stringify({ active: !target.isActive }) }),
    onSuccess: onChanged,
  });

  return (
    <section className="rounded-2xl border border-line bg-background p-4">
      <h3 className="text-base font-semibold">Monthly targets</h3>
      <p className="mt-1 max-w-3xl text-sm leading-6 text-muted">
        {targetType === "PtSessions"
          ? "This goal is measured from completed personal-training sessions in the selected month."
          : targetType === "NewPtClients"
            ? "This goal is measured from new client assignments in the selected month."
            : "This goal can be saved, but its actual stays unavailable until a personal-training payment is recorded."}
      </p>
      <div className="mt-4 grid gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,12rem)_7rem_auto] sm:items-end">
        <label className="grid gap-1 text-sm">Target name
          <input aria-label="Target name" className="field w-full" onChange={(event) => setName(event.target.value)} value={name} />
        </label>
        <label className="grid gap-1 text-sm">Measures
          <select aria-label="Target type" className="field w-full" onChange={(event) => setTargetType(event.target.value)} value={targetType}>
            <option value="NewPtClients">New PT clients</option>
            <option value="PtRevenue">PT revenue</option>
            <option value="PtSessions">PT sessions</option>
          </select>
        </label>
        <label className="grid gap-1 text-sm">Goal
          <input aria-label="Target value" className="field w-full" inputMode="decimal" onChange={(event) => setTargetValue(event.target.value)} value={targetValue} />
        </label>
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={save.isPending} onClick={() => save.mutate()} type="button">Add monthly target</button>
      </div>
      <ScrollableGrid className="mt-4">
        <ul className="grid w-max min-w-full gap-2">
          {targets.length === 0 ? <li className="text-sm text-muted">No targets for this period.</li> : null}
          {targets.map((item) => <TargetRow item={item} key={item.id} onToggle={() => toggle.mutate(item)} />)}
        </ul>
      </ScrollableGrid>
      {save.error instanceof ApiError ? <p className="mt-2" role="alert">{save.error.message}</p> : null}
    </section>
  );
}

function TargetRow({ item, onToggle }: { item: CoachTarget; onToggle: () => void }) {
  const label = targetLabels[item.targetType] ?? item.targetType;
  const percent = item.achievementPercent;
  return (
    <li className="min-w-[40rem] rounded-xl border border-line p-3 text-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="font-semibold">{item.name || label}</p>
          <p className="text-muted">{label} · {item.periodStart} to {item.periodEnd} · {item.isActive ? "Active" : "Inactive"}</p>
        </div>
        <button className="min-h-11 rounded-full border border-line px-3" onClick={onToggle} type="button">{item.isActive ? "Deactivate" : "Activate"}</button>
      </div>
      <p className="mt-3">{item.actual === null ? "Actual unavailable" : item.actual} of {item.targetValue}{percent === null ? "" : ` · ${percent}%`}</p>
      {percent === null ? <p className="mt-2 text-muted">Progress is not shown because this result is not stored.</p> : (
        <div aria-hidden="true" className="mt-2 h-2 overflow-hidden rounded-full bg-surface">
          <div className="h-2 rounded-full bg-accent" style={{ width: `${Math.min(100, Math.max(0, percent))}%` }} />
        </div>
      )}
    </li>
  );
}
