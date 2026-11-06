"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { DataTable } from "../../../components/DataTable";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";

type Settings = { pointsEnabled: boolean; defaultAwardPoints: number; requireApprovalForAwards: boolean; allowManualAdjustments: boolean };
type Definition = { id: string; name: string; description: string | null; metric: string; defaultGoal: number; period: string; defaultAwardPoints: number; status: string };
type Draft = { name: string; description: string; metric: string; defaultGoal: string; period: string; defaultAwardPoints: string; status: "Draft" | "Active" };

const emptyDraft: Draft = { name: "", description: "", metric: "NewPtClients", defaultGoal: "1", period: "Monthly", defaultAwardPoints: "10", status: "Active" };

export function CoachRewardsSection({ tenantId }: { tenantId: string | null }) {
  const client = useQueryClient();
  const [editing, setEditing] = useState<Definition | true | null>(null);
  const settings = useQuery({
    queryKey: ["trainer-reward-settings", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<Settings>("/configuration/trainer-rewards"),
  });
  const definitions = useQuery({
    queryKey: ["trainer-target-definitions", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<Definition[]>("/configuration/trainer-target-definitions"),
  });
  const saveSettings = useMutation({
    mutationFn: (body: Settings) => api("/configuration/trainer-rewards", { method: "PUT", body: JSON.stringify(body) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["trainer-reward-settings", tenantId] }); },
  });
  const statusChange = useMutation({
    mutationFn: ({ id, status }: { id: string; status: "Active" | "Inactive" }) => api(`/configuration/trainer-target-definitions/${id}/status`, { method: "POST", body: JSON.stringify({ status }) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["trainer-target-definitions"] }); },
  });

  const value = settings.data;

  return (
    <div className="mt-4 grid gap-8">
      <section>
        <h2 className="text-xl font-semibold">Recognition points</h2>
        <p className="mt-2 max-w-3xl text-sm text-muted">Points are recognition-only and non-redeemable. Balances come from approved ledger rows, not a mutable balance field.</p>
        {settings.isLoading ? <p className="mt-3 text-sm text-muted">Loading settings...</p> : null}
        {settings.error instanceof ApiError ? <p className="mt-3" role="alert">{settings.error.message}</p> : null}
        {value ? (
          <form
            className="mt-4 grid max-w-xl gap-3"
            onSubmit={(event) => {
              event.preventDefault();
              const data = new FormData(event.currentTarget);
              saveSettings.mutate({
                pointsEnabled: data.get("pointsEnabled") === "on",
                defaultAwardPoints: Number(data.get("defaultAwardPoints")),
                requireApprovalForAwards: data.get("requireApprovalForAwards") === "on",
                allowManualAdjustments: data.get("allowManualAdjustments") === "on",
              });
            }}
          >
            <label className="flex min-h-11 items-center gap-2 text-sm"><input defaultChecked={value.pointsEnabled} name="pointsEnabled" type="checkbox" /> Enable recognition points</label>
            <FormField label="Default award points"><input className="field" defaultValue={value.defaultAwardPoints} min={0} name="defaultAwardPoints" required type="number" /></FormField>
            <label className="flex min-h-11 items-center gap-2 text-sm"><input defaultChecked={value.requireApprovalForAwards} name="requireApprovalForAwards" type="checkbox" /> Require approval before points post</label>
            <label className="flex min-h-11 items-center gap-2 text-sm"><input defaultChecked={value.allowManualAdjustments} name="allowManualAdjustments" type="checkbox" /> Allow manual adjustments</label>
            <button className="min-h-11 w-fit rounded-full bg-accent px-4 font-semibold text-background" disabled={saveSettings.isPending} type="submit">Save point settings</button>
            {saveSettings.error instanceof ApiError ? <p role="alert">{saveSettings.error.message}</p> : null}
          </form>
        ) : null}
      </section>
      <section>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-xl font-semibold">Target definitions</h2>
            <p className="mt-2 max-w-3xl text-sm text-muted">Reusable coach targets. Assignments on the Coaches page snapshot the reward rule so later edits do not rewrite history.</p>
          </div>
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setEditing(true)} type="button">Add target definition</button>
        </div>
        {definitions.error instanceof ApiError ? <p className="mt-3" role="alert">{definitions.error.message}</p> : null}
        <div className="mt-4">
          <DataTable
            columns={[
              { header: "Name", cell: (row) => row.name },
              { header: "Metric", cell: (row) => row.metric },
              { header: "Goal", cell: (row) => String(row.defaultGoal) },
              { header: "Period", cell: (row) => row.period },
              { header: "Points", cell: (row) => String(row.defaultAwardPoints) },
              { header: "Status", cell: (row) => row.status },
              { header: "Actions", cell: (row) => (
                <div className="flex flex-wrap gap-2">
                  <button className="min-h-11 rounded-full border border-line px-3" onClick={() => setEditing(row)} type="button">Edit</button>
                  {row.status !== "Active" ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => statusChange.mutate({ id: row.id, status: "Active" })} type="button">Activate</button> : null}
                  {row.status === "Active" ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => statusChange.mutate({ id: row.id, status: "Inactive" })} type="button">Deactivate</button> : null}
                </div>
              ) },
            ]}
            empty="No coach target definitions yet."
            rowKey={(row) => row.id}
            rows={definitions.data ?? []}
          />
        </div>
      </section>
      {editing ? <DefinitionDialog initial={editing === true ? null : editing} onClose={() => setEditing(null)} tenantId={tenantId} /> : null}
    </div>
  );
}

function DefinitionDialog({ initial, onClose, tenantId }: { initial: Definition | null; onClose: () => void; tenantId: string | null }) {
  const client = useQueryClient();
  const [draft, setDraft] = useState<Draft>(initial ? {
    name: initial.name,
    description: initial.description ?? "",
    metric: initial.metric,
    defaultGoal: String(initial.defaultGoal),
    period: initial.period,
    defaultAwardPoints: String(initial.defaultAwardPoints),
    status: initial.status === "Draft" ? "Draft" : "Active",
  } : emptyDraft);
  const save = useMutation({
    mutationFn: () => {
      const body = {
        name: draft.name,
        description: draft.description || null,
        metric: draft.metric,
        defaultGoal: Number(draft.defaultGoal),
        period: draft.period,
        defaultAwardPoints: Number(draft.defaultAwardPoints),
        status: draft.status,
      };
      return initial
        ? api(`/configuration/trainer-target-definitions/${initial.id}`, { method: "PUT", body: JSON.stringify(body) })
        : api("/configuration/trainer-target-definitions", { method: "POST", body: JSON.stringify(body) });
    },
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["trainer-target-definitions", tenantId] });
      onClose();
    },
  });
  return (
    <FormDialog onCancel={onClose} title={initial ? "Edit target definition" : "Add target definition"}>
      <form className="grid gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
        <FormField label="Name"><input className="field" maxLength={120} onChange={(event) => setDraft({ ...draft, name: event.target.value })} required value={draft.name} /></FormField>
        <FormField label="Description"><textarea className="field min-h-20 py-2" maxLength={500} onChange={(event) => setDraft({ ...draft, description: event.target.value })} value={draft.description} /></FormField>
        <FormField label="Metric">
          <select className="field" onChange={(event) => setDraft({ ...draft, metric: event.target.value })} value={draft.metric}>
            <option value="NewPtClients">New PT clients</option>
            <option value="PtSessions">PT sessions</option>
            <option value="PtRevenue">PT revenue</option>
          </select>
        </FormField>
        <FormField label="Default goal"><input className="field" min={0.0001} onChange={(event) => setDraft({ ...draft, defaultGoal: event.target.value })} required step="any" type="number" value={draft.defaultGoal} /></FormField>
        <FormField label="Period">
          <select className="field" onChange={(event) => setDraft({ ...draft, period: event.target.value })} value={draft.period}>
            <option>Monthly</option>
            <option>Quarterly</option>
            <option>Custom</option>
          </select>
        </FormField>
        <FormField label="Default award points"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, defaultAwardPoints: event.target.value })} required type="number" value={draft.defaultAwardPoints} /></FormField>
        {!initial ? (
          <FormField label="Status">
            <select className="field" onChange={(event) => setDraft({ ...draft, status: event.target.value as Draft["status"] })} value={draft.status}>
              <option>Draft</option>
              <option>Active</option>
            </select>
          </FormField>
        ) : null}
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <FormActions cancel={onClose} pending={save.isPending} submitLabel={initial ? "Save definition" : "Create definition"} />
      </form>
    </FormDialog>
  );
}
