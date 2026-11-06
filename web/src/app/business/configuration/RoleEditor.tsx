"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { api, ApiError } from "../../../lib/api";
import type { AccessModule, AccessRole, AccessScope } from "../../../lib/accessRoles";

export function RoleEditor({ role, tenantId, onClose, onSaved }: { role?: AccessRole; tenantId: string | null; onClose: () => void; onSaved: () => Promise<void> }) {
  const catalog = useQuery({
    queryKey: ["access-role-catalog", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<AccessModule[]>("/access-roles/catalog"),
  });
  const [name, setName] = useState(role?.name ?? "");
  const [description, setDescription] = useState(role?.description ?? "");
  const [selected, setSelected] = useState<Set<string>>(new Set(role?.permissionCodes ?? []));
  const [scopes, setScopes] = useState<AccessScope[]>(role?.scopes ?? []);
  const [search, setSearch] = useState("");
  const [open, setOpen] = useState<string | null>(null);
  const modules = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (catalog.data ?? []).filter((module) => !term || module.name.toLowerCase().includes(term) || module.actions.some((action) => action.label.toLowerCase().includes(term) || action.action.toLowerCase().includes(term)));
  }, [catalog.data, search]);
  const preview = (catalog.data ?? []).flatMap((module) => module.actions.filter((action) => selected.has(action.code)).map((action) => `${module.name} · ${action.action}`));
  const save = useMutation({
    mutationFn: () => api(role ? `/access-roles/${role.id}` : "/access-roles", {
      method: role ? "PUT" : "POST",
      body: JSON.stringify({ name, description, permissionCodes: [...selected], scopes: scopePayload(catalog.data ?? [], scopes) }),
    }),
    onSuccess: onSaved,
  });

  function toggle(code: string) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });
  }

  function setModule(module: AccessModule, enabled: boolean) {
    setSelected((current) => {
      const next = new Set(current);
      for (const action of module.actions) {
        if (enabled) next.add(action.code);
        else next.delete(action.code);
      }
      return next;
    });
  }

  const title = role ? "Edit role" : "Create role";

  return (
    <div className="fixed inset-0 z-30" role="presentation">
      <button aria-label="Close role editor backdrop" className="absolute inset-0 bg-black/40" onClick={onClose} type="button" />
      <aside
        aria-labelledby="role-editor-title"
        aria-modal="true"
        className="absolute inset-y-0 right-0 flex w-full max-w-xl flex-col border-l border-line bg-background shadow-xl"
        role="dialog"
      >
        <div className="flex shrink-0 items-start justify-between gap-3 border-b border-line px-5 py-4">
          <div className="min-w-0">
            <p className="text-xs font-semibold tracking-[0.16em] text-accent uppercase">Roles & Permissions</p>
            <h2 className="mt-1 text-2xl font-semibold" id="role-editor-title">{title}</h2>
          </div>
          <button
            aria-label="Close"
            className="flex size-11 shrink-0 items-center justify-center rounded-xl border border-line"
            onClick={onClose}
            type="button"
          >
            <CloseIcon />
          </button>
        </div>

        <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
          <div className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden overscroll-contain px-5 py-4">
            <label className="block text-sm">Role name
              <input className="field mt-1 w-full" onChange={(event) => setName(event.target.value)} required value={name} />
            </label>
            <label className="mt-3 block text-sm">Description
              <textarea className="field mt-1 w-full" onChange={(event) => setDescription(event.target.value)} rows={3} value={description} />
            </label>
            <label className="mt-3 block text-sm">Search permissions
              <input className="field mt-1 w-full" onChange={(event) => setSearch(event.target.value)} placeholder="View, payroll, members..." value={search} />
            </label>
            <div className="mt-4 grid gap-3">
              {modules.map((module) => {
                const scope = scopes.find((item) => item.resource === module.key)?.scope ?? "Own";
                return (
                  <details className="rounded-2xl border border-line p-3" key={module.key} onToggle={(event) => setOpen((event.target as HTMLDetailsElement).open ? module.key : null)} open={open === module.key || search.trim().length > 0}>
                    <summary className="cursor-pointer font-semibold">{module.name}</summary>
                    <div className="mt-3 flex flex-wrap gap-2">
                      <button className="min-h-11 rounded-full border border-line px-3" onClick={() => setModule(module, true)} type="button">Select all</button>
                      <button className="min-h-11 rounded-full border border-line px-3" onClick={() => setModule(module, false)} type="button">Clear all</button>
                      {module.scoped ? (
                        <label className="text-sm">Record scope
                          <select className="ml-2 min-h-11 rounded-full border border-line px-3" onChange={(event) => setScopes((current) => [...current.filter((item) => item.resource !== module.key), { resource: module.key, scope: event.target.value as AccessScope["scope"] }])} value={scope}>
                            <option value="All">Business</option>
                            <option value="Branch">Branch</option>
                            <option value="Assigned">Assigned records</option>
                            <option value="Own">Own records</option>
                          </select>
                        </label>
                      ) : null}
                    </div>
                    <div className="mt-3 grid gap-2">
                      {module.actions.filter((action) => !search.trim() || action.label.toLowerCase().includes(search.trim().toLowerCase()) || action.action.toLowerCase().includes(search.trim().toLowerCase()) || module.name.toLowerCase().includes(search.trim().toLowerCase())).map((action) => (
                        <label className="flex min-h-11 items-center gap-2 rounded-2xl border border-line px-3" key={action.code}>
                          <input checked={selected.has(action.code)} onChange={() => toggle(action.code)} type="checkbox" />
                          <span><span className="text-xs text-muted">{action.action}</span><br />{action.label}</span>
                        </label>
                      ))}
                    </div>
                  </details>
                );
              })}
            </div>
            <div className="mt-4 rounded-2xl border border-line p-3">
              <h3 className="font-semibold">Permission preview</h3>
              <p className="mt-2 text-sm">{preview.length === 0 ? "No permissions selected." : preview.join(", ")}</p>
              <p className="mt-2 text-sm text-muted">View does not open every record. Member, attendance, PT, and payment scopes stay on the module.</p>
            </div>
            {save.error instanceof ApiError ? <p className="mt-3 text-red-700" role="alert">{save.error.message}</p> : null}
          </div>

          <div className="flex shrink-0 gap-2 border-t border-line px-5 py-4">
            <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" disabled={save.isPending} type="submit">
              {save.isPending ? "Saving…" : "Save role"}
            </button>
            <button className="min-h-11 rounded-full border border-line px-4" onClick={onClose} type="button">Cancel</button>
          </div>
        </form>
      </aside>
    </div>
  );
}

function CloseIcon() {
  return (
    <svg aria-hidden className="size-5" fill="none" stroke="currentColor" strokeLinecap="round" strokeWidth="1.75" viewBox="0 0 24 24">
      <path d="M6 6l12 12M18 6 6 18" />
    </svg>
  );
}

function scopePayload(modules: AccessModule[], scopes: AccessScope[]): AccessScope[] {
  return modules.filter((module) => module.scoped).map((module) => ({
    resource: module.key,
    scope: scopes.find((item) => item.resource === module.key)?.scope ?? "Own",
  }));
}
