"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { DataTable } from "../../../components/DataTable";
import { ScrollableGrid } from "../../../components/ScrollableGrid";
import { PageState } from "../../../components/AdminShell";
import { api, ApiError } from "../../../lib/api";
import { canRetry, formatDuration, formatWhen, jobListPath, jobRefreshInterval, jobStatuses, nextSort, summaryPath, type BackgroundJob, type BackgroundJobDetails, type BackgroundJobSummary, type JobListFilters } from "../../../lib/backgroundJobs";
import type { Page, PlatformTenant } from "../../../lib/types";
import { JobDetails, StatusBadge } from "./JobDetails";
import { SystemJobsPanel } from "./SystemJobsPanel";

const initialFilters: JobListFilters = { term: "", jobType: "", status: "", queue: "", tenantId: "", from: "", to: "", correlationId: "", page: 1, pageSize: 10, sort: "createdAt", descending: true };

export default function BackgroundJobsPage() {
  const client = useQueryClient();
  const [draft, setDraft] = useState(initialFilters);
  const [filters, setFilters] = useState(initialFilters);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<"retry" | "cancel" | null>(null);
  const [tab, setTab] = useState<"all" | "system">("all");
  const [manageDenied, setManageDenied] = useState(false);
  const searchTimer = useRef<number | undefined>(undefined);
  const tenants = useQuery({ queryKey: ["platform-tenants", "jobs"], retry: false, queryFn: () => api<Page<PlatformTenant>>("/platform/tenants?page=1&pageSize=100") });
  const summary = useQuery({
    queryKey: ["background-jobs", "summary", filters.tenantId],
    queryFn: () => api<BackgroundJobSummary>(summaryPath(filters.tenantId)),
    refetchInterval: (query) => jobRefreshInterval(query.state.data, true),
    refetchIntervalInBackground: false,
  });
  const interval = jobRefreshInterval(summary.data, true);
  const list = useQuery({
    queryKey: ["background-jobs", "list", filters],
    queryFn: () => api<Page<BackgroundJob>>(jobListPath(filters)),
    refetchInterval: interval,
    refetchIntervalInBackground: false,
  });
  const details = useQuery({
    queryKey: ["background-jobs", "details", selectedId],
    enabled: selectedId !== null,
    queryFn: () => api<BackgroundJobDetails>(`/background-jobs/${selectedId}`),
    refetchInterval: interval,
    refetchIntervalInBackground: false,
  });
  const retry = useMutation({
    mutationFn: (jobId: string) => api(`/background-jobs/${jobId}/retry`, { method: "POST" }),
    onSuccess: async () => {
      setConfirm(null);
      await client.invalidateQueries({ queryKey: ["background-jobs"] });
    },
  });
  const cancel = useMutation({
    mutationFn: (jobId: string) => api(`/background-jobs/${jobId}/cancel`, { method: "POST" }),
    onSuccess: async () => {
      setConfirm(null);
      await client.invalidateQueries({ queryKey: ["background-jobs"] });
    },
  });

  const denied = list.error instanceof ApiError && list.error.status === 403;
  const actionError = retry.error instanceof ApiError ? retry.error : cancel.error instanceof ApiError ? cancel.error : null;
  const actionMessage = actionError?.status === 403 ? "You do not have permission to manage background jobs." : actionError?.message ?? null;
  const empty = filters.term || filters.jobType || filters.status || filters.queue || filters.tenantId || filters.from || filters.to || filters.correlationId
    ? "No jobs match these filters."
    : "No background jobs have been recorded.";

  function onSearch(term: string) {
    setDraft((current) => ({ ...current, term }));
    window.clearTimeout(searchTimer.current);
    searchTimer.current = window.setTimeout(() => {
      setFilters((current) => ({ ...current, term, page: 1 }));
    }, 300);
  }

  function apply(patch: Partial<JobListFilters>) {
    const next = { ...draft, ...patch, page: patch.page ?? 1 };
    setDraft(next);
    setFilters(next);
  }

  return (
    <PageState error={denied ? "You do not have permission to view background jobs." : list.error instanceof ApiError ? list.error.message : null} loading={list.isLoading} onRetry={() => void list.refetch()}>
      <h1 className="text-3xl font-semibold">Background Jobs</h1>
      <p className="mt-2 max-w-3xl text-muted">Operational status for every background process. Job arguments and credentials are not shown.</p>
      <div className="mt-4 flex gap-2" role="tablist">
        <button aria-selected={tab === "all"} className={tab === "all" ? "min-h-11 rounded-full bg-accent px-4 font-semibold text-background" : "min-h-11"} onClick={() => setTab("all")} role="tab" type="button">All Jobs</button>
        <button aria-selected={tab === "system"} className={tab === "system" ? "min-h-11 rounded-full bg-accent px-4 font-semibold text-background" : "min-h-11"} onClick={() => setTab("system")} role="tab" type="button">System Jobs</button>
      </div>
      {tab === "system" ? <SystemJobsPanel /> : null}
      {tab === "all" ? <>
      {summary.data ? <Summary summary={summary.data} /> : null}
      <div className="mt-6 flex flex-wrap gap-3">
        <input aria-label="Search jobs" className="field max-w-sm" onChange={(event) => onSearch(event.target.value)} placeholder="Search job id or type" value={draft.term} />
        <input aria-label="Job type" className="field max-w-xs" onChange={(event) => apply({ jobType: event.target.value })} placeholder="Job type" value={draft.jobType} />
        <select aria-label="Status" className="field max-w-xs" onChange={(event) => apply({ status: event.target.value })} value={draft.status}>
          <option value="">All statuses</option>
          {jobStatuses.map((status) => <option key={status}>{status}</option>)}
        </select>
        <select aria-label="Queue" className="field max-w-xs" onChange={(event) => apply({ queue: event.target.value })} value={draft.queue}>
          <option value="">All queues</option>
          {(summary.data?.queues ?? []).map((queue) => <option key={queue}>{queue}</option>)}
        </select>
        {tenants.isSuccess ? (
          <select aria-label="Tenant" className="field max-w-xs" onChange={(event) => apply({ tenantId: event.target.value })} value={draft.tenantId}>
            <option value="">All tenants</option>
            {tenants.data.items.map((tenant) => <option key={tenant.id} value={tenant.id}>{tenant.name}</option>)}
          </select>
        ) : null}
        <input aria-label="Correlation ID" className="field max-w-xs" onChange={(event) => apply({ correlationId: event.target.value })} placeholder="Correlation ID" value={draft.correlationId} />
        <input aria-label="Created from" className="field max-w-xs" onChange={(event) => apply({ from: event.target.value })} type="date" value={draft.from} />
        <input aria-label="Created to" className="field max-w-xs" onChange={(event) => apply({ to: event.target.value })} type="date" value={draft.to} />
      </div>
      <ScrollableGrid className="mt-4 md:hidden">
      <div className="grid w-max min-w-full gap-3">
        {(list.data?.items ?? []).length === 0 ? <p className="rounded-2xl border border-line bg-surface p-6 text-muted">{empty}</p> : list.data?.items.map((job) => (
          <article className="rounded-2xl border border-line p-4" key={job.jobId}>
            <p className="font-semibold">{job.jobType || "Unknown job"}</p>
            <StatusBadge status={job.status} />
            <p className="mt-2 text-sm text-muted">{formatWhen(job.createdAt)} · {formatDuration(job.durationSeconds)}</p>
            <div className="mt-3 flex gap-3">
              <button className="min-h-11" onClick={() => setSelectedId(job.jobId)} type="button">Details</button>
              {canRetry(job.status) && !manageDenied ? <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => { setSelectedId(job.jobId); setConfirm("retry"); }} type="button">Retry</button> : null}
            </div>
          </article>
        ))}
        <div className="flex items-center gap-3">
          <button className="min-h-11" disabled={filters.page <= 1} onClick={() => apply({ page: filters.page - 1 })} type="button">Previous</button>
          <span>Page {filters.page} of {Math.max(1, Math.ceil((list.data?.totalCount ?? 0) / filters.pageSize))}</span>
          <button className="min-h-11" disabled={filters.page * filters.pageSize >= (list.data?.totalCount ?? 0)} onClick={() => apply({ page: filters.page + 1 })} type="button">Next</button>
        </div>
      </div>
      </ScrollableGrid>
      <div className="mt-4 hidden md:block">
        <DataTable
          columns={[
            { header: "Job Type", cell: (row) => row.jobType || "Unknown job" },
            { header: "Status", cell: (row) => <StatusBadge status={row.status} /> },
            { header: "Queue", cell: (row) => row.queue },
            { header: "Tenant", cell: (row) => row.tenantName || "—" },
            { header: "Created", cell: (row) => formatWhen(row.createdAt) },
            { header: "Scheduled", cell: (row) => formatWhen(row.scheduledAt) },
            { header: "Started", cell: (row) => formatWhen(row.startedAt) },
            { header: "Completed", cell: (row) => formatWhen(row.completedAt) },
            { header: "Duration", cell: (row) => formatDuration(row.durationSeconds) },
            { header: "Attempts", cell: (row) => String(row.attemptCount) },
            { header: "Correlation ID", cell: (row) => row.correlationId || "—" },
            { header: "Actions", cell: (row) => <button className="min-h-11" onClick={() => setSelectedId(row.jobId)} type="button">Details</button> },
          ]}
          empty={empty}
          paging={{ page: filters.page, pageSize: filters.pageSize, totalCount: list.data?.totalCount ?? 0, onPageChange: (page) => apply({ page }), onPageSizeChange: (pageSize) => apply({ pageSize, page: 1 }) }}
          rowKey={(row) => row.jobId}
          rows={list.data?.items ?? []}
          sorting={{ header: filters.sort === "status" ? "Status" : "Created", direction: filters.descending ? "desc" : "asc", sortable: ["Created", "Status"], onSort: (header) => { const next = nextSort(filters.sort, filters.descending, header); apply({ ...next, page: 1 }); } }}
        />
      </div>
      {details.error instanceof ApiError && details.error.status === 404 ? <p className="mt-4" role="alert">That background job was not found.</p> : null}
      {details.data ? (
        <JobDetails
          details={details.data}
          error={actionMessage}
          manageDenied={manageDenied}
          onCancel={() => setConfirm("cancel")}
          onClose={() => setSelectedId(null)}
          onRetry={() => setConfirm("retry")}
          pending={retry.isPending || cancel.isPending}
        />
      ) : null}
      {confirm && selectedId ? (
        <ConfirmDialog
          body={confirm === "retry" ? "Retry this failed background job?" : "Cancel this background job?"}
          confirmLabel={confirm === "retry" ? "Retry" : "Cancel job"}
          onCancel={() => setConfirm(null)}
          onConfirm={() => {
            const action = confirm === "retry" ? retry : cancel;
            action.mutate(selectedId, { onError: (error) => { if (error instanceof ApiError && error.status === 403) { setManageDenied(true); } setConfirm(null); } });
          }}
          pending={retry.isPending || cancel.isPending}
          title={confirm === "retry" ? "Retry job" : "Cancel job"}
        />
      ) : null}
      </> : null}
    </PageState>
  );
}

function Summary({ summary }: { summary: BackgroundJobSummary }) {
  const cards = [
    ["Queued", summary.queued, "Waiting to execute"],
    ["Processing", summary.processing, "Currently executing"],
    ["Scheduled", summary.scheduled, "Waiting for scheduled time"],
    ["Succeeded", summary.succeeded, "Completed successfully"],
    ["Failed", summary.failed, summary.failed === 0 ? "No failed jobs" : "Execution failed"],
  ] as const;
  return (
    <div className="mt-6 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map(([label, value, hint]) => (
        <article className={`rounded-2xl border bg-surface p-4 ${label === "Failed" && value > 0 ? "border-accent" : "border-line"}`} key={label}>
          <p className="text-sm text-muted">{label}</p>
          <p className={`mt-2 text-3xl font-semibold ${label === "Failed" && value > 0 ? "text-accent" : ""}`}>{value}</p>
          <p className="mt-1 text-sm text-muted">{hint}</p>
        </article>
      ))}
      <article className="rounded-2xl border border-line bg-surface p-4">
        <p className="text-sm text-muted">Jobs today</p>
        <p className="mt-2 text-3xl font-semibold">{summary.jobsToday}</p>
        <p className="mt-1 text-sm text-muted">{summary.jobsLast24Hours} in the last 24 hours</p>
      </article>
      <article className="rounded-2xl border border-line bg-surface p-4">
        <p className="text-sm text-muted">Average duration</p>
        <p className="mt-2 text-3xl font-semibold">{formatDuration(summary.averageDurationSeconds)}</p>
      </article>
    </div>
  );
}
