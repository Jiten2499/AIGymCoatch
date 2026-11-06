"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { DataTable } from "../../../components/DataTable";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { noPtPackagesMessage, ptPackageLabel, type PtPackageOption } from "../../../lib/ptPackage";

type Profile = { currency: string };
type Draft = { name: string; description: string; sessionCount: string; validityDays: string; price: string; status: "Draft" | "Active" };

const emptyDraft: Draft = { name: "", description: "", sessionCount: "", validityDays: "", price: "", status: "Active" };

export function PtPackagesSection({ tenantId }: { tenantId: string | null }) {
  const client = useQueryClient();
  const [term, setTerm] = useState("");
  const [status, setStatus] = useState("");
  const [editing, setEditing] = useState<PtPackageOption | true | null>(null);
  const packages = useQuery({
    queryKey: ["pt-packages", tenantId, "configuration"],
    enabled: tenantId !== null,
    queryFn: () => api<PtPackageOption[]>("/pt-packages"),
  });
  const statusChange = useMutation({
    mutationFn: ({ id, next }: { id: string; next: "Active" | "Inactive" }) => api(`/pt-packages/${id}/status`, { method: "POST", body: JSON.stringify({ id, status: next }) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["pt-packages"] }); },
  });
  const rows = useMemo(() => {
    const query = term.trim().toLowerCase();
    return (packages.data ?? []).filter((item) => {
      const itemStatus = item.status ?? (item.isActive ? "Active" : "Inactive");
      return (status === "" || itemStatus === status) && (query === "" || item.name.toLowerCase().includes(query));
    });
  }, [packages.data, status, term]);

  return (
    <div className="mt-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap gap-3">
          <FormField label="Search"><input className="field" onChange={(event) => setTerm(event.target.value)} placeholder="Package name" value={term} /></FormField>
          <FormField label="Status">
            <select className="field" onChange={(event) => setStatus(event.target.value)} value={status}>
              <option value="">All</option>
              <option>Draft</option>
              <option>Active</option>
              <option>Inactive</option>
            </select>
          </FormField>
        </div>
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setEditing(true)} type="button">Add PT package</button>
      </div>
      {packages.isLoading ? <p className="mt-4 text-sm text-muted">Loading PT packages...</p> : null}
      {packages.error instanceof ApiError ? <p className="mt-4" role="alert">{packages.error.message}</p> : null}
      {statusChange.error instanceof ApiError ? <p className="mt-4" role="alert">{statusChange.error.message}</p> : null}
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Package", cell: (row) => ptPackageLabel(row) },
            { header: "Description", cell: (row) => row.description || "—" },
            { header: "Status", cell: (row) => row.status ?? (row.isActive ? "Active" : "Inactive") },
            { header: "Actions", cell: (row) => {
              const current = row.status ?? (row.isActive ? "Active" : "Inactive");
              return (
                <div className="flex flex-wrap gap-2">
                  <button className="min-h-11 rounded-full border border-line px-3" onClick={() => setEditing(row)} type="button">Edit</button>
                  {current !== "Active" ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => statusChange.mutate({ id: row.id, next: "Active" })} type="button">Activate</button> : null}
                  {current === "Active" ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => statusChange.mutate({ id: row.id, next: "Inactive" })} type="button">Deactivate</button> : null}
                </div>
              );
            } },
          ]}
          empty={noPtPackagesMessage}
          rowKey={(row) => row.id}
          rows={rows}
        />
      </div>
      {rows.length === 0 && !packages.isLoading && (packages.data?.length ?? 0) === 0 ? (
        <button className="mt-3 min-h-11 rounded-full border border-line px-4" onClick={() => setEditing(true)} type="button">Add PT package</button>
      ) : null}
      {editing ? <PtPackageDialog initial={editing === true ? undefined : editing} onCancel={() => setEditing(null)} onSaved={async () => { setEditing(null); await client.invalidateQueries({ queryKey: ["pt-packages"] }); }} tenantId={tenantId} /> : null}
    </div>
  );
}

function PtPackageDialog({ initial, tenantId, onCancel, onSaved }: { initial?: PtPackageOption; tenantId: string | null; onCancel: () => void; onSaved: () => void }) {
  const profile = useQuery({ queryKey: ["business-profile", tenantId, "pt"], enabled: tenantId !== null, queryFn: () => api<Profile>("/business/profile") });
  const currency = initial?.currency || profile.data?.currency || "";
  const [draft, setDraft] = useState<Draft>(initial
    ? { name: initial.name, description: initial.description ?? "", sessionCount: String(initial.sessionCount), validityDays: String(initial.validityDays), price: String(initial.price), status: initial.status === "Draft" ? "Draft" : "Active" }
    : emptyDraft);
  const [message, setMessage] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => {
      const body = {
        id: initial?.id,
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        sessionCount: Number(draft.sessionCount),
        validityDays: Number(draft.validityDays),
        price: Number(draft.price),
        currency,
        status: initial ? undefined : draft.status,
      };
      return api(initial ? `/pt-packages/${initial.id}` : "/pt-packages", { method: initial ? "PUT" : "POST", body: JSON.stringify(body) });
    },
    onSuccess: () => onSaved(),
  });

  return (
    <FormDialog onCancel={onCancel} title={initial ? "Edit PT package" : "Add PT package"} wide>
      <form className="mt-4 grid gap-3" onSubmit={(event) => {
        event.preventDefault();
        if (!draft.name.trim() || draft.name.trim().length > 150 || Number(draft.sessionCount) <= 0 || Number(draft.validityDays) <= 0 || draft.price.trim() === "" || Number(draft.price) < 0 || currency.length !== 3) {
          setMessage("Enter a name up to 150 characters, a positive session count, a positive validity in days, a price of zero or more, and use the gym currency.");
          return;
        }
        setMessage(null);
        save.mutate();
      }}>
        <FormField label="Package name"><input className="field" maxLength={150} onChange={(event) => setDraft({ ...draft, name: event.target.value })} required value={draft.name} /></FormField>
        <FormField label="Description"><textarea className="field min-h-20 py-2" maxLength={1000} onChange={(event) => setDraft({ ...draft, description: event.target.value })} value={draft.description} /></FormField>
        <FormField label="Number of PT sessions"><input className="field" min={1} onChange={(event) => setDraft({ ...draft, sessionCount: event.target.value })} required step={1} type="number" value={draft.sessionCount} /></FormField>
        <FormField label="Validity in days"><input className="field" min={1} onChange={(event) => setDraft({ ...draft, validityDays: event.target.value })} required step={1} type="number" value={draft.validityDays} /></FormField>
        <FormField label="Package price">
          <span className="flex gap-2">
            <input className="field" min={0} onChange={(event) => setDraft({ ...draft, price: event.target.value })} required step="0.01" type="number" value={draft.price} />
            <input aria-label="Currency" className="field w-24" readOnly value={currency} />
          </span>
        </FormField>
        {initial ? null : (
          <FormField label="Status">
            <select className="field" onChange={(event) => setDraft({ ...draft, status: event.target.value as Draft["status"] })} value={draft.status}>
              <option>Draft</option>
              <option>Active</option>
            </select>
          </FormField>
        )}
        <p className="text-sm text-muted">Standalone packages stay separate from personal-training sessions included in a gym membership. Deactivating a package keeps existing assignments and hides it from new ones.</p>
        {message ? <p role="alert">{message}</p> : null}
        {profile.error instanceof ApiError && !initial ? <p role="alert">{profile.error.message}</p> : null}
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={save.isPending} submitLabel={initial ? "Save changes" : "Add PT package"} />
      </form>
    </FormDialog>
  );
}
