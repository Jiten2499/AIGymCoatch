"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { DataTable } from "../../../components/DataTable";
import { GridActionButton, GridActionGroup } from "../../../components/GridActionButton";
import { ProfileDeleteDialog } from "../../../components/ProfileDeleteDialog";
import { api, ApiError } from "../../../lib/api";
import type { GymMember, GymMemberDetails } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { getSession, subscribeSession } from "../../../lib/session";
import { EditMemberDialog } from "./EditMemberDialog";
import { AddMemberDialog } from "./MemberDialogs";
import { memberDetailsHref } from "./memberAccess";

export default function MembersPage() {
  const client = useQueryClient();
  const router = useRouter();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const params = useSearchParams();
  const statusFilter = params.get("status") ?? "";
  const [term, setTerm] = useState("");
  const [adding, setAdding] = useState(params.get("action") === "add");
  const [editing, setEditing] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [resetMember, setResetMember] = useState<GymMember | null>(null);
  const [resetNotice, setResetNotice] = useState<string | null>(null);
  const [deletedNotice, setDeletedNotice] = useState<string | null>(params.get("deleted") === "1" ? "The profile was deleted from active business views. The record is retained as soft-deleted data." : null);
  const members = useQuery({
    queryKey: ["members", tenantId, term, statusFilter],
    enabled: tenantId !== null,
    queryFn: () => api<Page<GymMember>>(`/members?page=1&pageSize=50&sort=name${statusFilter ? `&status=${encodeURIComponent(statusFilter)}` : ""}${term.trim() ? `&term=${encodeURIComponent(term.trim())}` : ""}`),
  });
  const details = useQuery({
    queryKey: ["member", tenantId, selected],
    enabled: tenantId !== null && selected !== null,
    queryFn: () => api<GymMemberDetails>(`/members/${selected}`),
  });
  const rows = members.data?.items ?? [];
  const remove = useMutation({
    mutationFn: (memberId: string) => api(`/members/${memberId}`, { method: "DELETE" }),
    onSuccess: async () => {
      const name = rows.find((row) => row.id === selected)?.fullName ?? "The member";
      setConfirmDelete(false);
      setSelected(null);
      setDeletedNotice(`${name} was deleted from active business views. The record is retained as soft-deleted data.`);
      await client.invalidateQueries({ queryKey: ["members"] });
      await client.invalidateQueries({ queryKey: ["trainers"] });
    },
  });
  const status = useMutation({
    mutationFn: (next: string) => api(`/members/${selected}/status`, { method: "PATCH", body: JSON.stringify({ status: next }) }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["members", tenantId] });
      await client.invalidateQueries({ queryKey: ["member", tenantId, selected] });
    },
  });
  const sendReset = useMutation({
    mutationFn: (userId: string) => api(`/business/users/${userId}/password-reset`, { method: "POST" }),
    onSuccess: () => {
      const name = resetMember?.fullName ?? "That member";
      const email = resetMember?.email ? ` (${resetMember.email})` : "";
      setResetNotice(`A password reset link was queued for ${name}${email}.`);
      setResetMember(null);
    },
  });

  return (
    <PageState error={members.error instanceof ApiError ? members.error.message : null} loading={members.isLoading} onRetry={() => void members.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Members</h1>
          <p className="mt-2 max-w-3xl text-muted">Customers of this gym. A login is created only when you enter an email. {members.data ? `${members.data.totalCount} total.` : ""}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {selected ? <Link className="inline-flex min-h-11 items-center rounded-full bg-accent px-4 font-semibold text-background" href={memberDetailsHref(selected)}>View Details</Link> : null}
          {selected ? <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setEditing(true)} type="button">Edit</button> : null}
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setAdding(true)} type="button">Add member</button>
        </div>
      </div>
      {deletedNotice ? <p className="mt-4" role="status">{deletedNotice}</p> : null}
      {resetNotice ? <p className="mt-4" role="status">{resetNotice}</p> : null}
      {sendReset.error instanceof ApiError ? <p className="mt-4" role="alert">{sendReset.error.message}</p> : null}
      <input aria-label="Search members" className="field mt-6 max-w-sm" onChange={(event) => setTerm(event.target.value)} placeholder="Search name or phone" value={term} />
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Name", cell: (row) => row.fullName },
            { header: "Email", cell: (row) => row.email || "—" },
            { header: "Phone", cell: (row) => row.mobile || "—" },
            { header: "Status", cell: (row) => row.status },
            { header: "Joined", cell: (row) => row.joinDate },
            { header: "Actions", cell: (row) => (
              <GridActionGroup>
                {row.userId && row.email ? (
                  <GridActionButton
                    action="reset"
                    onClick={(event) => {
                      event.stopPropagation();
                      setResetNotice(null);
                      sendReset.reset();
                      setResetMember(row);
                    }}
                  />
                ) : null}
                <GridActionButton action="delete" onClick={(event) => { event.stopPropagation(); setSelected(row.id); setConfirmDelete(true); }} />
              </GridActionGroup>
            ) },
          ]}
          empty="No members were returned."
          onRowClick={(row) => router.push(memberDetailsHref(row.id))}
          rowKey={(row) => row.id}
          rows={rows}
          selection={{ isSelected: (row) => selected === row.id, label: (row) => `Select ${row.fullName}`, onToggle: (row) => setSelected((current) => (current === row.id ? null : row.id)) }}
        />
      </div>
      {details.data ? (
        <section className="mt-6 rounded-2xl border border-line bg-surface p-6">
          <h2 className="text-xl font-semibold">{details.data.firstName} {details.data.lastName}</h2>
          <p className="mt-2 text-muted">{details.data.email} · {details.data.memberCode} · {details.data.status}</p>
          {status.error instanceof ApiError ? <p className="mt-3" role="alert">{status.error.message}</p> : null}
          <button className="mt-4 min-h-11 rounded-full border border-line px-4" onClick={() => status.mutate(details.data?.status === "Active" ? "Inactive" : "Active")} type="button">
            {details.data.status === "Active" ? "Mark inactive" : "Mark active"}
          </button>
        </section>
      ) : null}
      {adding ? <AddMemberDialog onCancel={() => setAdding(false)} onCreated={async () => { setAdding(false); await client.invalidateQueries({ queryKey: ["members", tenantId] }); }} /> : null}
      {editing && selected ? <EditMemberDialog memberId={selected} onCancel={() => setEditing(false)} onSaved={async () => { setEditing(false); await client.invalidateQueries({ queryKey: ["members", tenantId] }); await client.invalidateQueries({ queryKey: ["member", tenantId, selected] }); }} /> : null}
      {confirmDelete && selected ? (
        <ProfileDeleteDialog
          error={remove.error instanceof ApiError ? remove.error.message : null}
          name={rows.find((row) => row.id === selected)?.fullName ?? "this member"}
          onCancel={() => { setConfirmDelete(false); remove.reset(); }}
          onConfirm={() => remove.mutate(selected)}
          pending={remove.isPending}
          title="Delete member"
        />
      ) : null}
      {resetMember?.userId ? (
        <ConfirmDialog
          body={`Send a password reset email to ${resetMember.fullName}${resetMember.email ? ` at ${resetMember.email}` : ""}? The link expires after a short time and can be used once.`}
          confirmLabel="Send reset link"
          onCancel={() => { setResetMember(null); sendReset.reset(); }}
          onConfirm={() => {
            if (resetMember.userId) {
              sendReset.mutate(resetMember.userId);
            }
          }}
          pending={sendReset.isPending}
          title="Send password reset"
        />
      ) : null}
    </PageState>
  );
}
