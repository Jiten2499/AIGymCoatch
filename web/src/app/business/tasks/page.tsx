"use client";

import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { api, ApiError } from "../../../lib/api";
import { approvalStamp, type ApprovalRequestItem } from "../../../lib/approvals";
import type { Page } from "../../../lib/types";
import { getSession, subscribeSession } from "../../../lib/session";
import { ApprovalReview } from "./ApprovalReview";

export default function TasksPage() {
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const search = useSearchParams();
  const [status, setStatus] = useState("Pending");
  const [requestType, setRequestType] = useState("");
  const [requestedBy, setRequestedBy] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(search.get("requestId"));
  const requests = useQuery({
    queryKey: ["approval-requests", tenantId, status, requestType, requestedBy, from, to, page],
    enabled: tenantId !== null,
    retry: false,
    queryFn: () => {
      const params = new URLSearchParams({ page: String(page), pageSize: "20", sort: "createdAt" });
      if (status) {
        params.set("status", status);
      } else {
        params.set("includeAllStatuses", "true");
      }
      if (requestType) params.set("requestType", requestType);
      if (requestedBy.trim()) params.set("requestedByUserId", requestedBy.trim());
      if (from) params.set("from", from);
      if (to) params.set("to", to);
      return api<Page<ApprovalRequestItem>>(`/approval-requests?${params.toString()}`);
    },
  });
  const unauthorized = requests.error instanceof ApiError && requests.error.status === 403;
  const items = requests.data?.items ?? [];

  return (
    <section className="grid gap-4">
      <h1 className="text-2xl font-semibold">Tasks</h1>
      <p className="text-sm text-muted">Approval Requests</p>
      <div className="flex flex-wrap gap-2">
        <select className="field" onChange={(event) => { setPage(1); setStatus(event.target.value); }} value={status}>
          <option value="Pending">Pending</option>
          <option value="Approved">Approved</option>
          <option value="Rejected">Rejected</option>
          <option value="">All statuses</option>
        </select>
        <select className="field" onChange={(event) => { setPage(1); setRequestType(event.target.value); }} value={requestType}>
          <option value="">All types</option>
          <option value="MembershipFreeze">Membership freeze</option>
          <option value="MembershipExtension">Membership extension</option>
          <option value="TrainerRewardAward">Coach target credit</option>
          <option value="StaffLeave">Staff leave</option>
        </select>
        <input aria-label="Requested by" className="field" onChange={(event) => { setPage(1); setRequestedBy(event.target.value); }} placeholder="Requested by user id" value={requestedBy} />
        <input aria-label="From" className="field" onChange={(event) => { setPage(1); setFrom(event.target.value); }} type="date" value={from} />
        <input aria-label="To" className="field" onChange={(event) => { setPage(1); setTo(event.target.value); }} type="date" value={to} />
      </div>
      <PageState error={unauthorized ? "You do not have permission to review approval requests." : requests.isError ? "Approval requests could not be loaded." : null} loading={requests.isLoading} onRetry={() => void requests.refetch()}>
        {items.length === 0 ? <p>No approval requests match this view.</p> : (
          <div className="grid gap-3">
            {items.map((item) => (
              <article className="rounded-2xl border border-line bg-surface p-4" key={item.id}>
                <h2 className="font-semibold">{item.title}</h2>
                <p>{item.relatedLabel}</p>
                <p className="text-sm text-muted">Requested by {item.requestedByName}</p>
                <p className="text-sm">{item.summary}</p>
                <p className="text-sm">{approvalStamp(item.requestedAt)} · {item.status}</p>
                <button className="mt-3 min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setSelected(item.id)} type="button">Review</button>
              </article>
            ))}
          </div>
        )}
        {requests.data && requests.data.page * requests.data.pageSize < requests.data.totalCount ? (
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setPage((current) => current + 1)} type="button">Next page</button>
        ) : null}
      </PageState>
      {selected ? <ApprovalReview id={selected} onClose={() => setSelected(null)} tenantId={tenantId} /> : null}
    </section>
  );
}
