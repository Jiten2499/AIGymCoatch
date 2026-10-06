"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import { getSession, subscribeSession } from "../../../lib/session";
import type { CreatedWorkspaceUser, Page, WorkspaceUser } from "../../../lib/types";
import { AddUserDialog, AssignPlanDialog, EditUserDialog, PasswordDialog } from "./UserDialogs";

export default function BusinessUsersPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const users = useQuery({
    queryKey: ["workspace-users", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<WorkspaceUser>>("/business/users?page=1&pageSize=100"),
  });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [assigning, setAssigning] = useState(false);
  const [issued, setIssued] = useState<CreatedWorkspaceUser | null>(null);
  const rows = users.data?.items ?? [];
  const selectedRows = rows.filter((row) => selected.has(row.id));
  const remove = useMutation({
    mutationFn: (userIds: string[]) => api("/business/users/delete", { method: "POST", body: JSON.stringify({ userIds }) }),
    onSuccess: async () => {
      setConfirmDelete(false);
      setSelected(new Set());
      await client.invalidateQueries({ queryKey: ["workspace-users", tenantId] });
    },
  });

  function toggle(row: WorkspaceUser) {
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

  function toggleVisible(visible: WorkspaceUser[]) {
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
    <PageState error={users.error instanceof ApiError ? users.error.message : null} loading={users.isLoading} onRetry={() => void users.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Users</h1>
          <p className="mt-2 max-w-3xl text-muted">People who can sign in to this workspace. Select one person, then Assign plan, to give them a seat on a plan already assigned to this workspace. {users.data ? `${users.data.totalCount} total.` : ""}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAdding(true)} type="button">Add user</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setEditing(true)} type="button">Edit</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setAssigning(true)} type="button">Assign plan</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length === 0} onClick={() => setConfirmDelete(true)} type="button">Delete</button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => void users.refetch()} type="button">{users.isFetching ? "Refreshing..." : "Refresh"}</button>
        </div>
      </div>
      {remove.error instanceof ApiError ? <p className="mt-4" role="alert">{remove.error.message}</p> : null}
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Name", cell: (row) => `${row.firstName} ${row.lastName}` },
            { header: "Email", cell: (row) => row.email },
            { header: "Role", cell: (row) => row.role },
            { header: "Plan", cell: (row) => row.plan || "—" },
            { header: "Status", cell: (row) => row.status },
            { header: "Created", value: (row) => row.createdAt, cell: (row) => new Date(row.createdAt).toLocaleDateString() },
          ]}
          empty="No users were returned."
          rowKey={(row) => row.id}
          rows={rows}
          selection={{ isSelected: (row) => selected.has(row.id), label: (row) => `Select ${row.firstName} ${row.lastName}`, onToggle: toggle, onToggleVisible: toggleVisible }}
        />
      </div>
      {adding ? (
        <AddUserDialog
          onCancel={() => setAdding(false)}
          onCreated={async (created) => {
            setAdding(false);
            setIssued(created);
            await client.invalidateQueries({ queryKey: ["workspace-users", tenantId] });
          }}
        />
      ) : null}
      {assigning && selectedRows[0] ? (
        <AssignPlanDialog
          onCancel={() => setAssigning(false)}
          onSaved={async () => {
            setAssigning(false);
            await client.invalidateQueries({ queryKey: ["workspace-users", tenantId] });
            await client.invalidateQueries({ queryKey: ["workspace-plans"] });
          }}
          user={selectedRows[0]}
        />
      ) : null}
      {editing && selectedRows[0] ? (
        <EditUserDialog
          onCancel={() => setEditing(false)}
          onSaved={async () => {
            setEditing(false);
            await client.invalidateQueries({ queryKey: ["workspace-users", tenantId] });
          }}
          user={selectedRows[0]}
        />
      ) : null}
      {confirmDelete ? (
        <ConfirmDialog
          body={`Remove ${selectedRows.length} selected user${selectedRows.length === 1 ? "" : "s"} from this workspace? The workspace owner cannot be removed.`}
          confirmLabel="Delete"
          onCancel={() => setConfirmDelete(false)}
          onConfirm={() => remove.mutate(selectedRows.map((row) => row.id))}
          pending={remove.isPending}
          title="Delete users"
        />
      ) : null}
      {issued ? <PasswordDialog created={issued} onClose={() => setIssued(null)} /> : null}
    </PageState>
  );
}
