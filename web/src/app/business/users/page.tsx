"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { DataTable } from "../../../components/DataTable";
import { GridActionButton, GridActionGroup } from "../../../components/GridActionButton";
import { ProfileDeleteDialog } from "../../../components/ProfileDeleteDialog";
import { UserAvatar } from "../../../components/UserAvatar";
import { api, ApiError } from "../../../lib/api";
import { getSession, subscribeSession } from "../../../lib/session";
import type { CreatedWorkspaceUser, Page, WorkspaceUser } from "../../../lib/types";
import { AssignPlanDialog, InviteResultDialog, staffRoleLabel, type StaffRoleOption } from "./UserDialogs";
import { StaffWizard, type StaffWizardResult } from "./AddStaffWizard";
import { EditStaffWizard } from "./EditStaffWizard";

export default function BusinessUsersPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const users = useQuery({
    queryKey: ["workspace-users", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<WorkspaceUser>>("/business/users?page=1&pageSize=100"),
  });
  const roleCatalog = useQuery({
    queryKey: ["staff-roles", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<StaffRoleOption[]>("/business/users/roles"),
  });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [resetUser, setResetUser] = useState<WorkspaceUser | null>(null);
  const [deletedNotice, setDeletedNotice] = useState<string | null>(null);
  const [resetNotice, setResetNotice] = useState<string | null>(null);
  const [assigning, setAssigning] = useState(false);
  const [issued, setIssued] = useState<CreatedWorkspaceUser | null>(null);
  const [activationQueued, setActivationQueued] = useState(false);
  const [photoNotice, setPhotoNotice] = useState<string | null>(null);
  const [compensationHref, setCompensationHref] = useState<string | null>(null);
  const roleLabels = new Map((roleCatalog.data ?? []).map((role) => [role.name, role.label]));
  const rows = (users.data?.items ?? []).filter((row) => row.role !== "Trainer");
  function roleLabel(roleName: string) {
    return roleLabels.get(roleName) ?? staffRoleLabel(roleName);
  }
  const selectedRows = rows.filter((row) => selected.has(row.id));
  const remove = useMutation({
    mutationFn: (userIds: string[]) => api("/business/users/delete", { method: "POST", body: JSON.stringify({ userIds }) }),
    onSuccess: async () => {
      const names = selectedRows.map((row) => `${row.firstName} ${row.lastName}`).join(", ");
      setConfirmDelete(false);
      setSelected(new Set());
      setDeletedNotice(`${names} ${selectedRows.length === 1 ? "was" : "were"} deleted from active business views. The records are retained as soft-deleted data.`);
      await client.invalidateQueries({ queryKey: ["workspace-users"] });
      await client.invalidateQueries({ queryKey: ["coaches"] });
      await client.invalidateQueries({ queryKey: ["members"] });
    },
  });
  const sendReset = useMutation({
    mutationFn: (userId: string) => api(`/business/users/${userId}/password-reset`, { method: "POST" }),
    onSuccess: () => {
      const name = resetUser ? `${resetUser.firstName} ${resetUser.lastName}` : "That person";
      const email = resetUser?.email ? ` (${resetUser.email})` : "";
      setResetNotice(`A password reset link was queued for ${name}${email}.`);
      setResetUser(null);
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
          <h1 className="text-3xl font-semibold">Staff & Access</h1>
          <p className="mt-2 max-w-3xl text-muted">People who can sign in and run this gym: owners, admins, managers, receptionists, accountants, and custom roles. Coaches stay on the Coaches page. Gym customers stay on Members. {users.data ? `${rows.length} shown.` : ""}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAdding(true)} type="button">Add staff</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setEditing(true)} type="button">Edit</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setAssigning(true)} type="button">Assign plan</button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => void users.refetch()} type="button">{users.isFetching ? "Refreshing..." : "Refresh"}</button>
        </div>
      </div>
      {deletedNotice ? <p className="mt-4" role="status">{deletedNotice}</p> : null}
      {resetNotice ? <p className="mt-4" role="status">{resetNotice}</p> : null}
      {sendReset.error instanceof ApiError ? <p className="mt-4" role="alert">{sendReset.error.message}</p> : null}
      {photoNotice && !issued ? <p className="mt-4" role="alert">{photoNotice}</p> : null}
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Name", cell: (row) => <span className="flex items-center gap-2"><UserAvatar firstName={row.firstName} hasPhoto={row.hasPhoto} lastName={row.lastName} size={32} userId={row.id} />{row.firstName} {row.lastName}</span> },
            { header: "Email", cell: (row) => row.email },
            { header: "Role", cell: (row) => roleLabel(row.role) },
            { header: "Plan", cell: (row) => row.plan || "—" },
            { header: "Status", cell: (row) => row.status },
            { header: "Created", value: (row) => row.createdAt, cell: (row) => new Date(row.createdAt).toLocaleDateString() },
            { header: "Actions", cell: (row) => (
              <GridActionGroup>
                <GridActionButton action="reset" onClick={() => { setResetNotice(null); sendReset.reset(); setResetUser(row); }} />
                <GridActionButton action="delete" onClick={() => { setSelected(new Set([row.id])); setConfirmDelete(true); }} />
              </GridActionGroup>
            ) },
          ]}
          empty="No users were returned."
          rowKey={(row) => row.id}
          rows={rows}
          selection={{ isSelected: (row) => selected.has(row.id), label: (row) => `Select ${row.firstName} ${row.lastName}`, onToggle: toggle, onToggleVisible: toggleVisible }}
        />
      </div>
      {adding ? (
        <StaffWizard
          onCancel={() => setAdding(false)}
          onCreated={async (result: StaffWizardResult) => {
            setAdding(false);
            setIssued(result.user);
            setActivationQueued(result.activationQueued === true);
            setPhotoNotice(result.photoWarning ?? null);
            setCompensationHref(result.compensationHref ?? null);
            await client.invalidateQueries({ queryKey: ["workspace-users", tenantId] });
            await client.invalidateQueries({ queryKey: ["workspace-plans"] });
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
        <EditStaffWizard
          onCancel={() => setEditing(false)}
          onSaved={async (result) => {
            setEditing(false);
            if (result?.photoWarning) {
              setPhotoNotice(result.photoWarning);
            }
            await client.invalidateQueries({ queryKey: ["workspace-users", tenantId] });
            await client.invalidateQueries({ queryKey: ["workspace-plans"] });
            await client.invalidateQueries({ queryKey: ["staff-detail", tenantId, selectedRows[0]?.id] });
          }}
          userId={selectedRows[0].id}
        />
      ) : null}
      {confirmDelete ? (
        <ProfileDeleteDialog
          error={remove.error instanceof ApiError ? remove.error.message : null}
          name={selectedRows.map((row) => `${row.firstName} ${row.lastName}`).join(", ")}
          onCancel={() => { setConfirmDelete(false); remove.reset(); }}
          onConfirm={() => remove.mutate(selectedRows.map((row) => row.id))}
          pending={remove.isPending}
          title="Delete staff"
        />
      ) : null}
      {resetUser ? (
        <ConfirmDialog
          body={`Send a password reset email to ${resetUser.firstName} ${resetUser.lastName} at ${resetUser.email}? The link expires after a short time and can be used once.`}
          confirmLabel="Send reset link"
          onCancel={() => { setResetUser(null); sendReset.reset(); }}
          onConfirm={() => sendReset.mutate(resetUser.id)}
          pending={sendReset.isPending}
          title="Send password reset"
        />
      ) : null}
      {issued ? (
        <InviteResultDialog
          compensationHref={compensationHref}
          activationQueued={activationQueued}
          created={issued}
          notice={photoNotice}
          onClose={() => { setIssued(null); setPhotoNotice(null); setCompensationHref(null); setActivationQueued(false); }}
        />
      ) : null}
    </PageState>
  );
}
