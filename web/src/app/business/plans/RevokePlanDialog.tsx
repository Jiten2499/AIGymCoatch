"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import type { WorkspacePlan, WorkspacePlanAssignee } from "../../../lib/types";

export function RevokePlanDialog({
  plan,
  onCancel,
  onRevoked,
}: {
  plan: WorkspacePlan;
  onCancel: () => void;
  onRevoked: () => void;
}) {
  const assignees = useQuery({
    queryKey: ["workspace-plan-assignees", plan.subscriptionId],
    queryFn: () => api<WorkspacePlanAssignee[]>(`/business/plans/${plan.subscriptionId}/assignees`),
  });
  const [filter, setFilter] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const rows = useMemo(() => {
    const query = filter.trim().toLowerCase();
    const items = assignees.data ?? [];
    if (!query) {
      return items;
    }
    return items.filter((row) => {
      const name = `${row.firstName} ${row.lastName}`.toLowerCase();
      return name.includes(query) || row.email.toLowerCase().includes(query);
    });
  }, [assignees.data, filter]);
  const revoke = useMutation({
    mutationFn: () =>
      api(`/business/plans/${plan.subscriptionId}/revoke`, {
        method: "POST",
        body: JSON.stringify({ seatIds: [...selected] }),
      }),
    onSuccess: () => onRevoked(),
  });

  function toggle(seatId: string) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(seatId)) {
        next.delete(seatId);
      } else {
        next.add(seatId);
      }
      return next;
    });
  }

  function toggleVisible() {
    setSelected((current) => {
      const next = new Set(current);
      const ids = rows.map((row) => row.seatId);
      if (ids.length > 0 && ids.every((id) => next.has(id))) {
        ids.forEach((id) => next.delete(id));
      } else {
        ids.forEach((id) => next.add(id));
      }
      return next;
    });
  }

  return (
    <FormDialog onCancel={onCancel} title="Revoke plan" wide>
      <p className="text-muted">
        Select people who currently hold a seat on <strong>{plan.planName}</strong>. Revoking frees those seats for reassignment.
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
        {assignees.isLoading ? <p className="text-muted">Loading assignees…</p> : null}
        {assignees.error instanceof ApiError ? <p role="alert">{assignees.error.message}</p> : null}
        {!assignees.isLoading && !assignees.error && rows.length === 0 ? (
          <p className="text-muted">{(assignees.data?.length ?? 0) === 0 ? "No one is assigned to this plan." : "No assignees match that filter."}</p>
        ) : null}
        {rows.length > 0 ? (
          <div className="overflow-hidden rounded-xl border border-line">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-line bg-surface">
                <tr>
                  <th className="px-3 py-2">
                    <input
                      aria-label="Select visible assignees"
                      checked={rows.every((row) => selected.has(row.seatId))}
                      className="size-4"
                      onChange={toggleVisible}
                      type="checkbox"
                    />
                  </th>
                  <th className="px-3 py-2 font-medium">Name</th>
                  <th className="px-3 py-2 font-medium">Email</th>
                  <th className="px-3 py-2 font-medium">Assigned</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr className="border-b border-line last:border-0" key={row.seatId}>
                    <td className="px-3 py-2">
                      <input
                        aria-label={`Select ${row.firstName} ${row.lastName}`}
                        checked={selected.has(row.seatId)}
                        className="size-4"
                        onChange={() => toggle(row.seatId)}
                        type="checkbox"
                      />
                    </td>
                    <td className="px-3 py-2">{row.firstName} {row.lastName}</td>
                    <td className="px-3 py-2">{row.email}</td>
                    <td className="px-3 py-2">{new Date(row.assignedAt).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
        {revoke.error instanceof ApiError ? <p role="alert">{revoke.error.message}</p> : null}
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (selected.size > 0) {
              revoke.mutate();
            }
          }}
        >
          <div className="mt-2 flex justify-end gap-3">
            <button className="min-h-11 rounded-full px-4" onClick={onCancel} type="button">Cancel</button>
            <button
              className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60"
              disabled={selected.size === 0 || revoke.isPending}
              type="submit"
            >
              {revoke.isPending
                ? "Saving..."
                : selected.size
                  ? `Revoke ${selected.size} seat${selected.size === 1 ? "" : "s"}`
                  : "Revoke seats"}
            </button>
          </div>
        </form>
      </div>
    </FormDialog>
  );
}
