"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { UserAvatar } from "../../../components/UserAvatar";
import { api, ApiError } from "../../../lib/api";
import type { AccessAssignment, AccessRole, EffectiveAccess } from "../../../lib/accessRoles";

export function AssignedAccessPanel({ canManage, roles, tenantId }: { canManage: boolean; roles: AccessRole[]; tenantId: string | null }) {
  const client = useQueryClient();
  const [effectiveId, setEffectiveId] = useState<string | null>(null);
  const assignments = useQuery({
    queryKey: ["access-assignments", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<AccessAssignment[]>("/access-roles/assignments"),
  });
  const effective = useQuery({
    queryKey: ["access-effective", tenantId, effectiveId],
    enabled: effectiveId !== null,
    queryFn: () => api<EffectiveAccess>(`/access-roles/users/${effectiveId}`),
  });
  const assign = useMutation({
    mutationFn: (input: { userId: string; roleId: string }) => api("/access-roles/assignments", { method: "POST", body: JSON.stringify(input) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["access-assignments", tenantId] }); await client.invalidateQueries({ queryKey: ["access-roles", tenantId] }); },
  });
  const remove = useMutation({
    mutationFn: (userId: string) => api("/business/users/delete", { method: "POST", body: JSON.stringify({ userIds: [userId] }) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["access-assignments", tenantId] }); },
  });
  const choices = roles.filter((role) => role.isActive);

  return (
    <div className="mt-6">
      <p className="max-w-3xl text-muted">Assigning a different role keeps the same seat. Removing business access uses the existing staff removal flow.</p>
      {assignments.isLoading ? <p className="mt-4">Loading assigned users…</p> : null}
      {assignments.error instanceof ApiError ? <p className="mt-4 text-red-700" role="alert">{assignments.error.message}</p> : null}
      <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[760px] text-left">
          <thead>
            <tr className="border-b border-line text-sm text-muted">
              <th className="py-3">Person</th><th>Role</th><th>Seat</th><th>Account</th><th>Access</th>
            </tr>
          </thead>
          <tbody>
            {(assignments.data ?? []).map((person) => (
              <tr className="border-b border-line" key={person.userId}>
                <td className="py-3">
                  <div className="flex items-center gap-3">
                    <UserAvatar firstName={person.displayName} hasPhoto={person.hasPhoto} userId={person.userId} />
                    <div>
                      <p className="font-semibold">{person.displayName}</p>
                      <p className="text-sm text-muted">{person.email}</p>
                    </div>
                  </div>
                </td>
                <td>
                  {canManage ? (
                    <select aria-label={`Role for ${person.displayName}`} className="min-h-11 rounded-full border border-line px-3" onChange={(event) => assign.mutate({ userId: person.userId, roleId: event.target.value })} value={person.roleId ?? ""}>
                      <option value="" disabled>Select a role</option>
                      {choices.map((role) => <option key={role.id} value={role.id}>{role.label}</option>)}
                    </select>
                  ) : person.roleLabel}
                </td>
                <td>{person.seatStatus}</td>
                <td>{person.accountStatus}</td>
                <td>
                  <div className="flex flex-wrap gap-2">
                    <button className="min-h-11 rounded-full border border-line px-3" onClick={() => setEffectiveId(person.userId)} type="button">Effective permissions</button>
                    {canManage ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => remove.mutate(person.userId)} type="button">Remove access</button> : null}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {assign.error instanceof ApiError ? <p className="mt-3 text-red-700" role="alert">{assign.error.message}</p> : null}
      {remove.error instanceof ApiError ? <p className="mt-3 text-red-700" role="alert">{remove.error.message}</p> : null}
      {effectiveId ? (
        <div className="fixed inset-0 z-20 grid place-items-center bg-black/40 p-4" role="dialog">
          <div className="max-h-[80vh] w-full max-w-lg overflow-y-auto rounded-3xl bg-background p-6">
            <h2 className="text-xl font-semibold">{effective.data?.displayName || "Effective permissions"}</h2>
            <p className="mt-1 text-sm text-muted">{effective.data?.roleLabel}</p>
            {effective.isLoading ? <p className="mt-4">Loading…</p> : null}
            <ul className="mt-4 grid gap-2 text-sm">
              {(effective.data?.permissions ?? []).map((code) => <li key={code}>{code}</li>)}
            </ul>
            <button className="mt-4 min-h-11 rounded-full border border-line px-4" onClick={() => setEffectiveId(null)} type="button">Close</button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
