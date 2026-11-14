"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import { getSession, subscribeSession } from "../../../lib/session";
import type { WorkspacePlan } from "../../../lib/types";
import { AssignPlanDialog } from "./AssignPlanDialog";
import { RevokePlanDialog } from "./RevokePlanDialog";

export default function BusinessPlansPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const plans = useQuery({
    queryKey: ["workspace-plans", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<WorkspacePlan[]>("/business/plans"),
  });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [assigning, setAssigning] = useState(false);
  const [revoking, setRevoking] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const rows = plans.data ?? [];
  const selectedRows = rows.filter((row) => selected.has(row.subscriptionId));

  function toggle(row: WorkspacePlan) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(row.subscriptionId)) {
        next.delete(row.subscriptionId);
      } else {
        next.add(row.subscriptionId);
      }
      return next;
    });
  }

  function toggleVisible(visible: WorkspacePlan[]) {
    setSelected((current) => {
      const next = new Set(current);
      const ids = visible.map((row) => row.subscriptionId);
      if (ids.every((id) => next.has(id))) {
        ids.forEach((id) => next.delete(id));
      } else {
        ids.forEach((id) => next.add(id));
      }
      return next;
    });
  }

  return (
    <PageState error={plans.error instanceof ApiError ? plans.error.message : null} loading={plans.isLoading} onRetry={() => void plans.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Plans</h1>
          <p className="mt-2 max-w-3xl text-muted">Subscriptions assigned to this workspace. Seat counts are the licenses you can give to users.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            className="min-h-11 rounded-full border border-line px-4 disabled:opacity-50"
            disabled={selectedRows.length !== 1}
            onClick={() => { setNotice(null); setAssigning(true); }}
            type="button"
          >
            Assign plan
          </button>
          <button
            className="min-h-11 rounded-full border border-line px-4 disabled:opacity-50"
            disabled={selectedRows.length !== 1}
            onClick={() => { setNotice(null); setRevoking(true); }}
            type="button"
          >
            Revoke plan
          </button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => void plans.refetch()} type="button">
            {plans.isFetching ? "Refreshing..." : "Refresh"}
          </button>
        </div>
      </div>
      {notice ? <p className="mt-4" role="status">{notice}</p> : null}
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Plan", cell: (row) => row.planName },
            { header: "Code", cell: (row) => row.planCode },
            { header: "Status", cell: (row) => row.usable ? "Usable" : row.status },
            { header: "Starts", value: (row) => row.startsAt, cell: (row) => new Date(row.startsAt).toLocaleDateString() },
            { header: "Expires", value: (row) => row.expiresAt, cell: (row) => row.expiresAt ? new Date(row.expiresAt).toLocaleDateString() : "No expiry" },
            { header: "Seats", value: (row) => row.seats, cell: (row) => String(row.seats) },
            { header: "Assigned", value: (row) => row.assigned, cell: (row) => String(row.assigned) },
            { header: "Available", value: (row) => row.available, cell: (row) => String(row.available) },
            { header: "Features", cell: (row) => row.features.filter((feature) => feature.enabled).map((feature) => feature.code).join(", ") || "None" },
          ]}
          empty="No plans are assigned to this workspace."
          rowKey={(row) => row.subscriptionId}
          rows={rows}
          selection={{
            isSelected: (row) => selected.has(row.subscriptionId),
            label: (row) => `Select ${row.planName}`,
            onToggle: toggle,
            onToggleVisible: toggleVisible,
          }}
        />
      </div>
      {assigning && selectedRows[0] ? (
        <AssignPlanDialog
          onAssigned={async () => {
            const plan = selectedRows[0];
            setAssigning(false);
            setSelected(new Set());
            setNotice(`Seats on ${plan.planName} were assigned.`);
            await client.invalidateQueries({ queryKey: ["workspace-plans", tenantId] });
            await client.invalidateQueries({ queryKey: ["workspace-plan-candidates", plan.subscriptionId] });
            await client.invalidateQueries({ queryKey: ["workspace-plan-assignees", plan.subscriptionId] });
            await client.invalidateQueries({ queryKey: ["workspace-users"] });
          }}
          onCancel={() => setAssigning(false)}
          plan={selectedRows[0]}
        />
      ) : null}
      {revoking && selectedRows[0] ? (
        <RevokePlanDialog
          onCancel={() => setRevoking(false)}
          onRevoked={async () => {
            const plan = selectedRows[0];
            setRevoking(false);
            setSelected(new Set());
            setNotice(`Seats on ${plan.planName} were revoked.`);
            await client.invalidateQueries({ queryKey: ["workspace-plans", tenantId] });
            await client.invalidateQueries({ queryKey: ["workspace-plan-assignees", plan.subscriptionId] });
            await client.invalidateQueries({ queryKey: ["workspace-users"] });
          }}
          plan={selectedRows[0]}
        />
      ) : null}
    </PageState>
  );
}
