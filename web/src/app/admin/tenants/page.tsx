"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { PageState } from "../../../components/AdminShell";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { Page, PlatformTenant, TenantLicense } from "../../../lib/types";
import { AssignCatalogPlanDialog } from "../AssignPlanDialog";
import { AddTenantDialog, EditTenantDialog, PasswordDialog, type CreatedTenant } from "./TenantDialogs";

function planFor(tenantId: string, licenses: TenantLicense[]) {
  const rows = licenses.filter((license) => license.tenantId === tenantId);
  return rows.find((license) => license.usable)?.planCode ?? rows.find((license) => license.status === "Active")?.planCode ?? "—";
}

export default function TenantsPage() {
  const client = useQueryClient();
  const tenants = useQuery({ queryKey: ["platform-tenants"], queryFn: () => api<Page<PlatformTenant>>("/platform/tenants?page=1&pageSize=100") });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [issued, setIssued] = useState<CreatedTenant | null>(null);
  const [assigning, setAssigning] = useState(false);
  const licenses = useQuery({ queryKey: ["licenses"], queryFn: () => api<Page<TenantLicense>>("/platform/licenses?page=1&pageSize=100") });
  const rows = tenants.data?.items ?? [];
  const selectedRows = rows.filter((row) => selected.has(row.id));
  const remove = useMutation({
    mutationFn: (tenantIds: string[]) => api("/platform/tenants/delete", { method: "POST", body: JSON.stringify({ tenantIds }) }),
    onSuccess: async () => {
      setConfirmDelete(false);
      setSelected(new Set());
      await client.invalidateQueries({ queryKey: ["platform-tenants"] });
    },
  });

  function toggle(row: PlatformTenant) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(row.id)) {
        next.delete(row.id);
      } else {
        next.add(row.id);
      }
      return next;
    });
  }

  function toggleVisible(visible: PlatformTenant[]) {
    setSelected((current) => {
      const next = new Set(current);
      const ids = visible.map((row) => row.id);
      if (ids.every((id) => next.has(id))) {
        ids.forEach((id) => next.delete(id));
      } else {
        ids.forEach((id) => next.add(id));
      }
      return next;
    });
  }

  return (
    <PageState error={tenants.error instanceof ApiError ? tenants.error.message : null} loading={tenants.isLoading} onRetry={() => void tenants.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Tenants</h1>
          <p className="mt-2 max-w-3xl text-muted">Business workspaces are listed here. The platform workspace stays in place. {tenants.data ? `${tenants.data.totalCount} total.` : ""}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAdding(true)} type="button">Add business</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setEditing(true)} type="button">Edit</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setAssigning(true)} type="button">Assign plan</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length === 0} onClick={() => setConfirmDelete(true)} type="button">Delete</button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => void tenants.refetch()} type="button">{tenants.isFetching ? "Refreshing..." : "Refresh"}</button>
        </div>
      </div>
      {remove.error instanceof ApiError ? <p className="mt-4" role="alert">{remove.error.message}</p> : null}
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Name", cell: (row) => row.name },
            { header: "Type", cell: (row) => row.tenantType },
            { header: "Plan", cell: (row) => planFor(row.id, licenses.data?.items ?? []) },
            { header: "Status", cell: (row) => row.status },
          ]}
          empty="No tenants were returned."
          rowKey={(row) => row.id}
          rows={rows}
          selection={{ isSelected: (row) => selected.has(row.id), label: (row) => `Select ${row.name}`, onToggle: toggle, onToggleVisible: toggleVisible }}
        />
      </div>
      {adding ? (
        <AddTenantDialog
          onCancel={() => setAdding(false)}
          onCreated={async (created) => {
            setAdding(false);
            setIssued(created);
            await client.invalidateQueries({ queryKey: ["platform-tenants"] });
          }}
        />
      ) : null}
      {editing && selectedRows[0] ? (
        <EditTenantDialog
          onCancel={() => setEditing(false)}
          onSaved={async () => {
            setEditing(false);
            await client.invalidateQueries({ queryKey: ["platform-tenants"] });
          }}
          tenant={selectedRows[0]}
        />
      ) : null}
      {confirmDelete ? (
        <ConfirmDialog
          body={`Delete ${selectedRows.length} selected workspace${selectedRows.length === 1 ? "" : "s"}? The platform workspace cannot be deleted.`}
          confirmLabel="Delete"
          onCancel={() => setConfirmDelete(false)}
          onConfirm={() => remove.mutate(selectedRows.map((row) => row.id))}
          pending={remove.isPending}
          title="Delete tenants"
        />
      ) : null}
      {assigning && selectedRows[0] ? (
        <AssignCatalogPlanDialog
          audience="business"
          description={`Choose the plan for ${selectedRows[0].name}. Seat capacity follows the plan.`}
          onCancel={() => setAssigning(false)}
          onSaved={async () => {
            setAssigning(false);
            await client.invalidateQueries({ queryKey: ["licenses"] });
            await client.invalidateQueries({ queryKey: ["platform-tenants"] });
          }}
          path={`/platform/tenants/${selectedRows[0].id}/plan`}
          title="Assign plan"
        />
      ) : null}
      {issued ? <PasswordDialog created={issued} onClose={() => setIssued(null)} /> : null}
    </PageState>
  );
}
