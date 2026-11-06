"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { api, ApiError } from "../../../lib/api";
import type { AccessRole, AccessRoleList } from "../../../lib/accessRoles";
import { AssignedAccessPanel } from "./AssignedAccessPanel";
import { AccessOverviewPanel } from "./AccessOverviewPanel";
import { RoleEditor } from "./RoleEditor";

const subtabs = ["Roles & Permissions", "Assigned Users", "Access Overview"] as const;

export function RolesPermissionsSection({ tenantId }: { tenantId: string | null }) {
  const client = useQueryClient();
  const [subtab, setSubtab] = useState<(typeof subtabs)[number]>("Roles & Permissions");
  const [editing, setEditing] = useState<AccessRole | null | "new">(null);
  const [copying, setCopying] = useState<AccessRole | null>(null);
  const [copyName, setCopyName] = useState("");
  const roles = useQuery({
    queryKey: ["access-roles", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<AccessRoleList>("/access-roles"),
  });
  const duplicate = useMutation({
    mutationFn: () => api(`/access-roles/${copying?.id}/duplicate`, { method: "POST", body: JSON.stringify({ name: copyName }) }),
    onSuccess: async () => {
      setCopying(null);
      await client.invalidateQueries({ queryKey: ["access-roles", tenantId] });
      await client.invalidateQueries({ queryKey: ["staff-roles"] });
    },
  });
  const status = useMutation({
    mutationFn: (role: AccessRole) => api(`/access-roles/${role.id}/status`, { method: "POST", body: JSON.stringify({ active: !role.isActive }) }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["access-roles", tenantId] });
      await client.invalidateQueries({ queryKey: ["staff-roles"] });
    },
  });
  const data = roles.data;

  return (
    <section className="mt-8">
      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Roles and permissions">
        {subtabs.map((item) => (
          <button aria-selected={subtab === item} className={`min-h-11 rounded-full px-4 ${subtab === item ? "bg-accent font-semibold text-background" : "border border-line"}`} key={item} onClick={() => setSubtab(item)} role="tab" type="button">{item}</button>
        ))}
      </div>
      {roles.error instanceof ApiError ? <p className="mt-4 text-red-700" role="alert">{roles.error.message}</p> : null}
      {subtab === "Roles & Permissions" ? (
        <div className="mt-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="max-w-3xl text-muted">Standard roles are templates. Duplicate one to customize it for this business. A plan that includes every feature still does not grant these permissions, and changing a role does not use another seat.</p>
            {data?.canManage ? <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setEditing("new")} type="button">Create role</button> : null}
          </div>
          {roles.isLoading ? <p className="mt-4">Loading roles…</p> : null}
          <div className="mt-4 grid gap-4">
            {data?.roles.map((role) => (
              <article className="rounded-3xl border border-line p-4" key={role.id}>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h2 className="text-xl font-semibold">{role.label}</h2>
                    <p className="mt-1 text-sm text-muted">{role.description || "No description"}</p>
                  </div>
                  <p className="text-sm">{role.assignedUsers} assigned</p>
                </div>
                <p className="mt-3 text-sm">{role.permissionSummary}</p>
                <div className="mt-3 flex flex-wrap gap-2 text-xs">
                  {role.isSystem ? <span className="rounded-full border border-line px-3 py-1">Standard</span> : <span className="rounded-full border border-line px-3 py-1">Custom</span>}
                  {role.isProtected ? <span className="rounded-full border border-line px-3 py-1">Protected</span> : null}
                  <span className="rounded-full border border-line px-3 py-1">{role.isActive ? "Active" : "Inactive"}</span>
                </div>
                {data.canManage ? (
                  <div className="mt-4 flex flex-wrap gap-2">
                    {!role.isSystem ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => setEditing(role)} type="button">Edit</button> : null}
                    {!role.isProtected ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => { setCopying(role); setCopyName(`${role.label} copy`); }} type="button">Duplicate</button> : null}
                    {!role.isSystem ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => status.mutate(role)} type="button">{role.isActive ? "Deactivate" : "Activate"}</button> : null}
                  </div>
                ) : null}
                {status.error instanceof ApiError && status.variables?.id === role.id ? <p className="mt-2 text-red-700" role="alert">{status.error.message}</p> : null}
              </article>
            ))}
          </div>
        </div>
      ) : null}
      {subtab === "Assigned Users" ? <AssignedAccessPanel canManage={data?.canManage === true} roles={data?.roles ?? []} tenantId={tenantId} /> : null}
      {subtab === "Access Overview" ? <AccessOverviewPanel tenantId={tenantId} /> : null}
      {editing ? (
        <RoleEditor
          role={editing === "new" ? undefined : editing}
          tenantId={tenantId}
          onClose={() => setEditing(null)}
          onSaved={async () => {
            setEditing(null);
            await client.invalidateQueries({ queryKey: ["access-roles", tenantId] });
            await client.invalidateQueries({ queryKey: ["staff-roles"] });
          }}
        />
      ) : null}
      {copying ? (
        <div className="fixed inset-0 z-20 grid place-items-center bg-black/40 p-4" role="dialog">
          <form className="w-full max-w-md rounded-3xl bg-background p-6" onSubmit={(event) => { event.preventDefault(); duplicate.mutate(); }}>
            <h2 className="text-xl font-semibold">Duplicate {copying.label}</h2>
            <label className="mt-4 block text-sm">New role name
              <input className="field mt-1 w-full" onChange={(event) => setCopyName(event.target.value)} value={copyName} />
            </label>
            {duplicate.error instanceof ApiError ? <p className="mt-2 text-red-700" role="alert">{duplicate.error.message}</p> : null}
            <div className="mt-4 flex gap-2">
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" type="submit">Create copy</button>
              <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setCopying(null)} type="button">Cancel</button>
            </div>
          </form>
        </div>
      ) : null}
    </section>
  );
}
