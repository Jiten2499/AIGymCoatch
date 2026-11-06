"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../../components/AdminShell";
import { ProfileDeleteDialog } from "../../../../components/ProfileDeleteDialog";
import { api, ApiError } from "../../../../lib/api";
import type { GymMemberDetails } from "../../../../lib/gym";
import { getSession, subscribeSession, type WorkspaceSummary } from "../../../../lib/session";
import { canReadPayments, canRecordPtSession, canUpdateMember } from "../memberAccess";
import { EditMemberDialog } from "../EditMemberDialog";
import { MemberPanels } from "../MemberPanels";
import { MemberTraining } from "../MemberTraining";

const tabs = ["Overview", "Membership", "PT & Training", "Payments", "Attendance", "Progress"] as const;

export default function MemberDetailsPage() {
  const params = useParams<{ id: string }>();
  const memberId = params.id;
  const router = useRouter();
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const [tab, setTab] = useState<(typeof tabs)[number]>("Overview");
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const workspaces = useQuery({ queryKey: ["workspaces"], queryFn: () => api<WorkspaceSummary[]>("/tenants/me") });
  const roles = workspaces.data?.find((workspace) => workspace.id === tenantId)?.roles ?? [];
  const member = useQuery({
    queryKey: ["member", tenantId, memberId],
    enabled: tenantId !== null && Boolean(memberId),
    queryFn: () => api<GymMemberDetails>(`/members/${memberId}`),
  });
  const remove = useMutation({
    mutationFn: () => api(`/members/${memberId}`, { method: "DELETE" }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["members"] });
      router.push("/business/members?deleted=1");
    },
  });

  return (
    <PageState error={member.error instanceof ApiError ? member.error.message : null} loading={member.isLoading} onRetry={() => void member.refetch()}>
      <Link className="text-sm text-muted" href="/business/members">Back to members</Link>
      <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-3xl font-semibold">{member.data ? `${member.data.firstName} ${member.data.lastName}` : "Member"}</h1>
        {member.data && canUpdateMember(roles) ? (
          <div className="flex gap-2">
            <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setEditing(true)} type="button">Edit</button>
            <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setConfirmDelete(true)} type="button">Delete</button>
          </div>
        ) : null}
      </div>
      <p className="mt-2 text-muted">{member.data ? `${member.data.memberCode} · ${member.data.status}` : "This member was not found in the current workspace."}</p>
      <div className="mt-4 flex flex-wrap gap-2" role="tablist">
        {tabs.map((item) => (
          <button aria-selected={tab === item} className={`min-h-11 rounded-full px-3 ${tab === item ? "bg-accent font-semibold text-background" : "border border-line"}`} key={item} onClick={() => setTab(item)} role="tab" type="button">{item}</button>
        ))}
      </div>
      {member.data && tab === "PT & Training" ? (
        <MemberTraining canAssign={canUpdateMember(roles)} canRecord={canRecordPtSession(roles)} memberId={memberId} tenantId={tenantId} trainerId={member.data.assignedTrainerId ?? null} />
      ) : null}
      {member.data && tab !== "PT & Training" ? (
        <MemberPanels canPay={canReadPayments(roles)} canUpdate={canUpdateMember(roles)} member={member.data} memberId={memberId} tab={tab} tenantId={tenantId} />
      ) : null}
      {editing && member.data ? <EditMemberDialog memberId={memberId} onCancel={() => setEditing(false)} onSaved={() => setEditing(false)} /> : null}
      {confirmDelete && member.data ? (
        <ProfileDeleteDialog
          error={remove.error instanceof ApiError ? remove.error.message : null}
          name={`${member.data.firstName} ${member.data.lastName}`}
          onCancel={() => { setConfirmDelete(false); remove.reset(); }}
          onConfirm={() => remove.mutate()}
          pending={remove.isPending}
          title="Delete member"
        />
      ) : null}
    </PageState>
  );
}
