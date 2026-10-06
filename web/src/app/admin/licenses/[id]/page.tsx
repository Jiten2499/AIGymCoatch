"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useState } from "react";
import { PageState } from "../../../../components/AdminShell";
import { ConfirmDialog } from "../../../../components/ConfirmDialog";
import { api, ApiError } from "../../../../lib/api";
import type { Assignment, MemberOption, TenantLicense } from "../../../../lib/types";

export default function LicenseDetailPage() {
  const params = useParams<{ id: string }>();
  const client = useQueryClient();
  const licenses = useQuery({ queryKey: ["license", params.id], queryFn: () => api<TenantLicense>(`/platform/licenses/${params.id}`) });
  const license = licenses.data ?? null;
  const members = useQuery({ queryKey: ["members", license?.tenantId], queryFn: () => api<MemberOption[]>(`/platform/tenants/${license?.tenantId}/members`), enabled: Boolean(license) });
  const assignments = useQuery({ queryKey: ["assignments", license?.tenantId], queryFn: () => api<Assignment[]>(`/platform/tenants/${license?.tenantId}/license-assignments`), enabled: Boolean(license) });
  const [expires, setExpires] = useState("");
  const [userId, setUserId] = useState("");
  const [pendingSeat, setPendingSeat] = useState<string | null>(null);

  const extend = useMutation({
    mutationFn: () => api(`/platform/licenses/${params.id}/extend`, { method: "POST", body: JSON.stringify({ expiresAt: expires ? new Date(expires).toISOString() : null }) }),
    onSuccess: async () => client.invalidateQueries({ queryKey: ["license", params.id] }),
  });
  const assign = useMutation({
    mutationFn: () => api(`/platform/tenants/${license?.tenantId}/license-assignments`, { method: "POST", body: JSON.stringify({ userId }) }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["assignments", license?.tenantId] });
      await client.invalidateQueries({ queryKey: ["license", params.id] });
    },
  });
  const revokeSeat = useMutation({
    mutationFn: (id: string) => api(`/platform/license-assignments/${id}/revoke`, { method: "POST" }),
    onSuccess: async () => {
      setPendingSeat(null);
      await client.invalidateQueries({ queryKey: ["assignments", license?.tenantId] });
      await client.invalidateQueries({ queryKey: ["license", params.id] });
    },
  });
  const error = [licenses, members, assignments, extend, assign, revokeSeat].map((item) => item.error).find(Boolean);

  return (
    <PageState error={licenses.isError ? (error instanceof ApiError ? error.message : "The subscription could not be loaded.") : null} loading={licenses.isLoading} onRetry={() => void licenses.refetch()}>
      <h1 className="text-3xl font-semibold">{license?.tenantName ?? "Subscription"}</h1>
      {license ? <p className="mt-2 text-muted">{license.planCode} · {license.usable ? "Usable" : license.status} · {license.assigned} assigned · {license.available} available · {license.baseCapacity} seats</p> : <p>That subscription was not found.</p>}
      {error instanceof ApiError && !licenses.isError ? <p className="mt-4" role="alert">{error.message}</p> : null}
      <form className="mt-6 flex flex-wrap gap-3" onSubmit={(event) => { event.preventDefault(); extend.mutate(); }}>
        <input aria-label="New expiry" className="field" onChange={(event) => setExpires(event.target.value)} type="date" value={expires} />
        <button className="min-h-11 rounded-full border border-line px-4" disabled={extend.isPending} type="submit">Extend subscription</button>
      </form>
      <h2 className="mt-8 text-xl font-semibold">Seat assignments</h2>
      <form className="mt-3 flex flex-wrap gap-3" onSubmit={(event) => { event.preventDefault(); assign.mutate(); }}>
        <select aria-label="Workspace member" className="field" onChange={(event) => setUserId(event.target.value)} value={userId}>
          <option value="">Choose a member</option>
          {(members.data ?? []).map((member) => <option key={member.userId} value={member.userId}>{member.firstName} {member.lastName} · {member.email}</option>)}
        </select>
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" disabled={!userId || assign.isPending} type="submit">Assign seat</button>
      </form>
      <ul className="mt-4 grid gap-2">
        {(assignments.data ?? []).map((item) => (
          <li className="flex items-center justify-between gap-3" key={item.id}>
            <span>{(members.data ?? []).find((member) => member.userId === item.userId)?.email ?? item.userId} · {item.status}</span>
            {item.status === "Active" ? <button className="min-h-11" onClick={() => setPendingSeat(item.id)} type="button">Release seat</button> : null}
          </li>
        ))}
      </ul>
      {pendingSeat ? <ConfirmDialog body="Release this seat assignment?" confirmLabel="Release seat" onCancel={() => setPendingSeat(null)} onConfirm={() => revokeSeat.mutate(pendingSeat)} pending={revokeSeat.isPending} title="Release seat" /> : null}
    </PageState>
  );
}
