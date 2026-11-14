"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import type { WorkspacePlan, WorkspacePlanCandidate } from "../../../lib/types";

export function AssignPlanDialog({
  plan,
  onCancel,
  onAssigned,
}: {
  plan: WorkspacePlan;
  onCancel: () => void;
  onAssigned: () => void;
}) {
  const candidates = useQuery({
    queryKey: ["workspace-plan-candidates", plan.subscriptionId],
    queryFn: () => api<WorkspacePlanCandidate[]>(`/business/plans/${plan.subscriptionId}/candidates`),
  });
  const [filter, setFilter] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const rows = useMemo(() => {
    const query = filter.trim().toLowerCase();
    const items = candidates.data ?? [];
    if (!query) {
      return items;
    }
    return items.filter((row) => {
      const name = `${row.firstName} ${row.lastName}`.toLowerCase();
      return name.includes(query) || row.email.toLowerCase().includes(query);
    });
  }, [candidates.data, filter]);
  const overCapacity = selected.size > plan.available;
  const assign = useMutation({
    mutationFn: () =>
      api(`/business/plans/${plan.subscriptionId}/assign`, {
        method: "POST",
        body: JSON.stringify({ userIds: [...selected] }),
      }),
    onSuccess: () => onAssigned(),
  });

  function toggle(userId: string) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(userId)) {
        next.delete(userId);
      } else {
        next.add(userId);
      }
      return next;
    });
  }

  function toggleVisible() {
    setSelected((current) => {
      const next = new Set(current);
      const ids = rows.map((row) => row.userId);
      if (ids.length > 0 && ids.every((id) => next.has(id))) {
        ids.forEach((id) => next.delete(id));
      } else {
        ids.forEach((id) => next.add(id));
      }
      return next;
    });
  }

  return (
    <FormDialog onCancel={onCancel} title="Assign plan" wide>
      <p className="text-muted">
        Select people to give a seat on <strong>{plan.planName}</strong>. {plan.available} seat{plan.available === 1 ? "" : "s"} available. Assigning replaces a person's current plan.
      </p>
      <div className="mt-4 grid gap-3">
        <FormField label="Filter users">
          <input
            aria-label="Filter users"
            className="field"
            onChange={(event) => setFilter(event.target.value)}
            placeholder="Search by name or email"
            value={filter}
          />
        </FormField>
        {candidates.isLoading ? <p className="text-muted">Loading people…</p> : null}
        {candidates.error instanceof ApiError ? <p role="alert">{candidates.error.message}</p> : null}
        {!candidates.isLoading && !candidates.error && rows.length === 0 ? (
          <p className="text-muted">{(candidates.data?.length ?? 0) === 0 ? "Everyone who can receive a seat already has this plan." : "No people match that filter."}</p>
        ) : null}
        {rows.length > 0 ? (
          <div className="overflow-hidden rounded-xl border border-line">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-line bg-surface">
                <tr>
                  <th className="px-3 py-2">
                    <input
                      aria-label="Select visible people"
                      checked={rows.every((row) => selected.has(row.userId))}
                      className="size-4"
                      onChange={toggleVisible}
                      type="checkbox"
                    />
                  </th>
                  <th className="px-3 py-2 font-medium">Name</th>
                  <th className="px-3 py-2 font-medium">Email</th>
                  <th className="px-3 py-2 font-medium">Current plan</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr className="border-b border-line last:border-0" key={row.userId}>
                    <td className="px-3 py-2">
                      <input
                        aria-label={`Select ${row.firstName} ${row.lastName}`}
                        checked={selected.has(row.userId)}
                        className="size-4"
                        onChange={() => toggle(row.userId)}
                        type="checkbox"
                      />
                    </td>
                    <td className="px-3 py-2">{row.firstName} {row.lastName}</td>
                    <td className="px-3 py-2">{row.email}</td>
                    <td className="px-3 py-2">{row.currentPlan || "None"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
        {overCapacity ? <p role="alert">Select at most {plan.available} people. Only {plan.available} seats are free.</p> : null}
        {assign.error instanceof ApiError ? <p role="alert">{assign.error.message}</p> : null}
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (selected.size > 0 && !overCapacity) {
              assign.mutate();
            }
          }}
        >
          <div className="mt-2 flex justify-end gap-3">
            <button className="min-h-11 rounded-full px-4" onClick={onCancel} type="button">Cancel</button>
            <button
              className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60"
              disabled={selected.size === 0 || overCapacity || assign.isPending || !plan.usable}
              type="submit"
            >
              {assign.isPending
                ? "Saving..."
                : selected.size
                  ? `Assign ${selected.size} seat${selected.size === 1 ? "" : "s"}`
                  : "Assign seats"}
            </button>
          </div>
        </form>
      </div>
    </FormDialog>
  );
}
