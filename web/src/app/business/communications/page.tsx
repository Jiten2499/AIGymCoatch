"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { CommunicationAudienceType, CommunicationDetails, CommunicationRow, CommunicationStatus, CommunicationType } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { getSession, subscribeSession } from "../../../lib/session";
import { CommunicationInbox } from "./CommunicationInbox";
import { CommunicationWizard } from "./CommunicationWizard";
import { canCancelCommunication, canEditCommunication, canPublishNow, formatInZone } from "./schedule";

const types: Array<CommunicationType | ""> = ["", "Announcement", "Circular", "Offer", "Event", "Alert", "General"];
const statuses: Array<CommunicationStatus | ""> = ["", "Draft", "Scheduled", "Published", "Archived"];
const audiences: Array<CommunicationAudienceType | ""> = ["", "AllMembers", "ActiveMembers", "ExpiringMembers", "MembershipPackage", "TrainerMembers", "SelectedMembers", "AllTrainers", "AllStaff", "AllBusinessUsers", "SelectedTrainers", "SelectedStaff"];

export default function CommunicationsPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const profile = useQuery({
    queryKey: ["business-profile", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<{ timeZone: string }>("/business/profile"),
  });
  const timeZone = profile.data?.timeZone || "UTC";
  const [term, setTerm] = useState("");
  const [type, setType] = useState<CommunicationType | "">("");
  const [status, setStatus] = useState<CommunicationStatus | "">("");
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<CommunicationDetails | null>(null);
  const [selected, setSelected] = useState<CommunicationRow | null>(null);
  const [audience, setAudience] = useState<CommunicationAudienceType | "">("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const unread = useQuery({
    queryKey: ["communication-unread", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<number>("/communications/inbox/unread-count"),
  });
  const list = useQuery({
    queryKey: ["communications", tenantId, term, type, status, audience, from, to, page],
    enabled: tenantId !== null,
    queryFn: () => {
      const params = new URLSearchParams({ page: String(page), pageSize: "10" });
      if (term.trim()) params.set("term", term.trim());
      if (type) params.set("type", type);
      if (status) params.set("status", status);
      if (audience) params.set("audienceType", audience);
      if (from) params.set("publishFrom", new Date(from).toISOString());
      if (to) params.set("publishTo", new Date(`${to}T23:59:59`).toISOString());
      return api<Page<CommunicationRow>>(`/communications?${params}`);
    },
  });
  const detail = useQuery({
    queryKey: ["communication", tenantId, selected?.id],
    enabled: tenantId !== null && selected !== null,
    queryFn: () => api<CommunicationDetails>(`/communications/${selected!.id}`),
  });
  const publish = useMutation({
    mutationFn: (id: string) => api(`/communications/${id}/publish`, { method: "POST" }),
    onSuccess: async () => {
      setSelected(null);
      await client.invalidateQueries({ queryKey: ["communications", tenantId] });
    },
  });
  const archive = useMutation({
    mutationFn: (id: string) => api(`/communications/${id}/archive`, { method: "POST" }),
    onSuccess: async () => {
      setSelected(null);
      await client.invalidateQueries({ queryKey: ["communications", tenantId] });
    },
  });

  const denied = list.error instanceof ApiError && list.error.status === 403;
  const total = list.data?.totalCount ?? 0;
  return (
    <PageState error={denied ? "You do not have permission to view circulars." : list.error instanceof ApiError ? list.error.message : null} loading={list.isLoading} onRetry={() => void list.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Circulars{unread.data ? ` (${unread.data})` : ""}</h1>
          <p className="mt-2 text-muted">Announcements, offers, events, and alerts for members in this gym.</p>
        </div>
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setCreating(true)} type="button">
          Create circular
        </button>
      </div>
      <div className="mt-6 flex flex-wrap gap-3">
        <input aria-label="Search circulars" className="field max-w-sm" onChange={(event) => setTerm(event.target.value)} placeholder="Search title or message" value={term} />
        <select aria-label="Filter by type" className="field max-w-xs" onChange={(event) => { setType(event.target.value as CommunicationType | ""); setPage(1); }} value={type}>
          {types.map((value) => <option key={value || "all"} value={value}>{value || "All types"}</option>)}
        </select>
        <select aria-label="Filter by status" className="field max-w-xs" onChange={(event) => { setStatus(event.target.value as CommunicationStatus | ""); setPage(1); }} value={status}>
          {statuses.map((value) => <option key={value || "all"} value={value}>{value || "All statuses"}</option>)}
        </select>
        <select aria-label="Filter by audience" className="field max-w-xs" onChange={(event) => { setAudience(event.target.value as CommunicationAudienceType | ""); setPage(1); }} value={audience}>
          {audiences.map((value) => <option key={value || "all"} value={value}>{value || "All audiences"}</option>)}
        </select>
        <input aria-label="Publish from" className="field max-w-xs" onChange={(event) => { setFrom(event.target.value); setPage(1); }} type="date" value={from} />
        <input aria-label="Publish to" className="field max-w-xs" onChange={(event) => { setTo(event.target.value); setPage(1); }} type="date" value={to} />
      </div>
      {(publish.error instanceof ApiError || archive.error instanceof ApiError) ? (
        <p className="mt-3" role="alert">{(publish.error instanceof ApiError ? publish.error.message : null) ?? (archive.error instanceof ApiError ? archive.error.message : null)}</p>
      ) : null}
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Title", cell: (row) => row.title },
            { header: "Type", cell: (row) => row.type },
            { header: "Audience", cell: (row) => row.audienceType },
            { header: "Status", cell: (row) => row.status },
            { header: "Scheduled", cell: (row) => row.status === "Scheduled" ? formatInZone(row.publishAt, timeZone) : "—" },
            { header: "Publish", cell: (row) => formatInZone(row.publishAt, timeZone) },
            { header: "Expires", cell: (row) => row.expiresAt ? new Date(row.expiresAt).toLocaleString() : "—" },
            { header: "Created by", cell: (row) => row.createdBy || "—" },
          ]}
          empty="No circulars were returned."
          rowKey={(row) => row.id}
          rows={list.data?.items ?? []}
          selection={{ isSelected: (row) => selected?.id === row.id, label: (row) => `Select ${row.title}`, onToggle: (row) => setSelected((current) => (current?.id === row.id ? null : row)) }}
        />
      </div>
      <div className="mt-4 flex flex-wrap items-center gap-3">
        <button className="min-h-11 rounded-full border border-line px-4" disabled={page <= 1} onClick={() => setPage((current) => current - 1)} type="button">Previous</button>
        <span>Page {page} · {total} circulars</span>
        <button className="min-h-11 rounded-full border border-line px-4" disabled={page * 10 >= total} onClick={() => setPage((current) => current + 1)} type="button">Next</button>
      </div>
      {detail.data ? (
        <article className="mt-4 rounded-2xl border border-line p-4">
          <p className="text-sm text-muted">{detail.data.type} · {detail.data.audienceType} · {detail.data.status}</p>
          <h2 className="mt-1 text-lg font-semibold">{detail.data.title}</h2>
          <p className="mt-2 whitespace-pre-wrap">{detail.data.message}</p>
          {detail.data.imageStorageKey ? <p className="mt-2 text-sm text-muted">Image attached</p> : null}
        </article>
      ) : null}
      {selected && detail.data && canEditCommunication(selected.status) ? (
        <button className="mt-4 min-h-11 rounded-full border border-line px-4" onClick={() => setEditing(detail.data)} type="button">Edit</button>
      ) : null}
      {selected && canPublishNow(selected.status) ? (
        <button className="mt-4 ml-2 min-h-11 rounded-full border border-line px-4" onClick={() => publish.mutate(selected.id)} type="button">Publish now</button>
      ) : null}
      {selected && canCancelCommunication(selected.status) ? (
        <button className="mt-4 ml-2 min-h-11 rounded-full border border-line px-4" onClick={() => archive.mutate(selected.id)} type="button">{selected.status === "Scheduled" ? "Cancel" : "Archive"}</button>
      ) : null}
      <CommunicationInbox tenantId={tenantId} />
      {creating || editing ? (
        <CommunicationWizard
          initial={editing}
          timeZone={timeZone}
          onCancel={() => { setCreating(false); setEditing(null); }}
          onSaved={async () => {
            setCreating(false);
            setEditing(null);
            await client.invalidateQueries({ queryKey: ["communications", tenantId] });
            await client.invalidateQueries({ queryKey: ["communication", tenantId] });
          }}
        />
      ) : null}
    </PageState>
  );
}
