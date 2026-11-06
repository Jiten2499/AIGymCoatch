"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { ScrollableGrid } from "../../../components/ScrollableGrid";
import { api, ApiError } from "../../../lib/api";
import type { CommunicationType, InboxCommunication } from "../../../lib/gym";
import type { Page } from "../../../lib/types";

const filters: Array<{ id: string; label: string; type?: CommunicationType; unread?: boolean }> = [
  { id: "all", label: "All" },
  { id: "unread", label: "Unread", unread: true },
  { id: "Announcement", label: "Announcements", type: "Announcement" },
  { id: "Circular", label: "Circulars", type: "Circular" },
  { id: "Offer", label: "Offers", type: "Offer" },
  { id: "Event", label: "Events", type: "Event" },
  { id: "Alert", label: "Alerts", type: "Alert" },
];

export function CommunicationInbox({ tenantId }: { tenantId: string | null }) {
  const client = useQueryClient();
  const [filter, setFilter] = useState(filters[0]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const list = useQuery({
    queryKey: ["communication-inbox", tenantId, filter.id],
    enabled: tenantId !== null,
    queryFn: () => {
      const params = new URLSearchParams({ page: "1", pageSize: "30" });
      if (filter.type) params.set("type", filter.type);
      if (filter.unread) params.set("unreadOnly", "true");
      return api<Page<InboxCommunication>>(`/communications/inbox?${params}`);
    },
  });
  const detail = useQuery({
    queryKey: ["communication-inbox-item", tenantId, selectedId],
    enabled: tenantId !== null && selectedId !== null,
    queryFn: () => api<InboxCommunication>(`/communications/inbox/${selectedId}`),
  });
  const markRead = useMutation({
    mutationFn: (id: string) => api(`/communications/inbox/${id}/read`, { method: "POST" }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["communication-inbox", tenantId] });
      await client.invalidateQueries({ queryKey: ["communication-inbox-item", tenantId] });
      await client.invalidateQueries({ queryKey: ["communication-unread", tenantId] });
    },
  });
  const markAll = useMutation({
    mutationFn: () => api("/communications/inbox/read-all", { method: "POST" }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["communication-inbox", tenantId] });
      await client.invalidateQueries({ queryKey: ["communication-inbox-item", tenantId] });
      await client.invalidateQueries({ queryKey: ["communication-unread", tenantId] });
    },
  });

  function open(item: InboxCommunication) {
    setSelectedId(item.id);
    if (!item.isRead) markRead.mutate(item.id);
  }

  return (
    <section className="mt-10">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-semibold">My circulars</h2>
        <button className="min-h-11 rounded-full border border-line px-4" onClick={() => markAll.mutate()} type="button">Mark all read</button>
      </div>
      <div className="mt-4 flex flex-wrap gap-2">
        {filters.map((item) => (
          <button className={`min-h-11 rounded-full px-4 ${filter.id === item.id ? "bg-accent text-background" : "border border-line"}`} key={item.id} onClick={() => setFilter(item)} type="button">{item.label}</button>
        ))}
      </div>
      {list.error instanceof ApiError ? <p className="mt-3" role="alert">{list.error.message}</p> : null}
      {list.isLoading ? <p className="mt-3 text-muted">Loading circulars…</p> : null}
      {list.isSuccess && (list.data.items ?? []).length === 0 ? <p className="mt-3 text-muted">No circulars for this filter.</p> : null}
      <ScrollableGrid className="mt-4">
      <ul className="grid w-max min-w-full gap-3">
        {(list.data?.items ?? []).map((item) => (
          <li key={item.id}>
            <button className="w-full rounded-2xl border border-line p-4 text-left" onClick={() => open(item)} type="button">
              <span className="text-sm text-muted">{item.type} · {new Date(item.publishAt).toLocaleString()} · {item.isRead ? "Read" : "Unread"}</span>
              <span className="mt-1 block font-semibold">{item.title}</span>
              <span className="mt-1 block text-sm">{item.message}</span>
            </button>
          </li>
        ))}
      </ul>
      </ScrollableGrid>
      {detail.data ? (
        <article className="mt-4 rounded-2xl border border-line p-4">
          <p className="text-sm text-muted">{detail.data.type} · {new Date(detail.data.publishAt).toLocaleString()} · {detail.data.isRead ? "Read" : "Unread"}</p>
          <h3 className="mt-1 text-lg font-semibold">{detail.data.title}</h3>
          <p className="mt-2 whitespace-pre-wrap">{detail.data.message}</p>
        </article>
      ) : null}
    </section>
  );
}
