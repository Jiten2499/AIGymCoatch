"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { LeadDetails, LeadRow } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { getSession, subscribeSession } from "../../../lib/session";
import { ConvertLeadDialog } from "./ConvertLeadDialog";
import { LeadDialog } from "./LeadDialog";

type CrmSummary = { totalLeads: number; followUpsToday: number; converted: number; lost: number };

export default function LeadsPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const params = useSearchParams();
  const followUp = params.get("followUp") ?? "";
  const [term, setTerm] = useState("");
  const [adding, setAdding] = useState(params.get("action") === "add");
  const [converting, setConverting] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const leads = useQuery({
    queryKey: ["leads", tenantId, term, followUp],
    enabled: tenantId !== null,
    queryFn: () => api<Page<LeadRow>>(`/leads?page=1&pageSize=50&sort=created${followUp ? `&followUp=${encodeURIComponent(followUp)}` : ""}${term.trim() ? `&term=${encodeURIComponent(term.trim())}` : ""}`),
  });
  const summary = useQuery({
    queryKey: ["leads-summary", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<CrmSummary>("/leads/summary"),
  });
  const details = useQuery({
    queryKey: ["lead", tenantId, selected],
    enabled: selected !== null,
    queryFn: () => api<LeadDetails>(`/leads/${selected}`),
  });
  const action = useMutation({
    mutationFn: (kind: "lost" | "reopen") => {
      if (kind === "lost") {
        return api(`/leads/${selected}/lost`, { method: "POST", body: JSON.stringify({ reason: "Other", notes: null }) });
      }
      return api(`/leads/${selected}/reopen`, { method: "POST" });
    },
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["leads", tenantId] });
      await client.invalidateQueries({ queryKey: ["lead", tenantId, selected] });
      await client.invalidateQueries({ queryKey: ["leads-summary", tenantId] });
    },
  });

  const canConvert = details.data && details.data.status !== "Converted" && details.data.status !== "Lost";

  return (
    <PageState error={leads.error instanceof ApiError ? leads.error.message : null} loading={leads.isLoading} onRetry={() => void leads.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Leads</h1>
          <p className="mt-2 text-muted">
            {summary.data ? `${summary.data.totalLeads} leads · ${summary.data.followUpsToday} follow-ups today · ${summary.data.converted} converted · ${summary.data.lost} lost` : "People who have asked about joining."}
          </p>
        </div>
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAdding(true)} type="button">Add lead</button>
      </div>
      <input aria-label="Search leads" className="field mt-6 max-w-sm" onChange={(event) => setTerm(event.target.value)} placeholder="Search name or mobile" value={term} />
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Name", cell: (row) => row.fullName },
            { header: "Mobile", cell: (row) => row.mobile },
            { header: "Source", cell: (row) => row.source },
            { header: "Status", cell: (row) => row.status },
            { header: "Package", cell: (row) => row.interestedPackage || "—" },
            { header: "Assigned", cell: (row) => row.assignedTo || "—" },
          ]}
          empty="No leads were returned."
          rowKey={(row) => row.id}
          rows={leads.data?.items ?? []}
          selection={{ isSelected: (row) => selected === row.id, label: (row) => `Select ${row.fullName}`, onToggle: (row) => setSelected((current) => (current === row.id ? null : row.id)) }}
        />
      </div>
      {details.data ? (
        <section className="mt-6 rounded-2xl border border-line bg-surface p-6">
          <h2 className="text-xl font-semibold">{details.data.fullName}</h2>
          <p className="mt-2 text-muted">{details.data.mobile} · {details.data.status} · {details.data.notes || "No notes"}</p>
          {details.data.status === "Converted" ? (
            <p className="mt-2">Converted to member {details.data.convertedMemberId ?? "—"}{details.data.convertedBy ? ` by ${details.data.convertedBy}` : ""}{details.data.convertedAt ? ` on ${details.data.convertedAt}` : ""}.</p>
          ) : null}
          {action.error instanceof ApiError ? <p className="mt-3" role="alert">{action.error.message}</p> : null}
          <div className="mt-4 flex flex-wrap gap-2">
            {canConvert ? <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setConverting(true)} type="button">Convert</button> : null}
            <button className="min-h-11 rounded-full border border-line px-4" onClick={() => action.mutate("lost")} type="button">Mark lost</button>
            <button className="min-h-11 rounded-full border border-line px-4" onClick={() => action.mutate("reopen")} type="button">Reopen</button>
          </div>
        </section>
      ) : null}
      {adding ? <LeadDialog onCancel={() => setAdding(false)} onCreated={async () => { setAdding(false); await client.invalidateQueries({ queryKey: ["leads", tenantId] }); await client.invalidateQueries({ queryKey: ["leads-summary", tenantId] }); }} /> : null}
      {converting && details.data ? (
        <ConvertLeadDialog
          lead={details.data}
          onCancel={() => setConverting(false)}
          onConverted={async () => {
            setConverting(false);
            await client.invalidateQueries({ queryKey: ["leads", tenantId] });
            await client.invalidateQueries({ queryKey: ["lead", tenantId, selected] });
            await client.invalidateQueries({ queryKey: ["leads-summary", tenantId] });
            await client.invalidateQueries({ queryKey: ["members"] });
          }}
        />
      ) : null}
    </PageState>
  );
}
