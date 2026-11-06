"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { DataTable } from "../../../components/DataTable";
import { ScrollableGrid } from "../../../components/ScrollableGrid";
import { PageState } from "../../../components/AdminShell";
import { api, ApiError } from "../../../lib/api";
import { formatDuration, formatWhen } from "../../../lib/backgroundJobs";
import { systemJobActions, type SystemJob, type SystemJobDetails } from "../../../lib/systemJobs";

export function SystemJobsPanel() {
  const client = useQueryClient();
  const [selected, setSelected] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<"disable" | "run" | null>(null);
  const [manageDenied, setManageDenied] = useState(false);
  const list = useQuery({ queryKey: ["system-jobs"], queryFn: () => api<SystemJob[]>("/system-jobs") });
  const details = useQuery({
    queryKey: ["system-jobs", selected],
    enabled: selected !== null,
    queryFn: () => api<SystemJobDetails>(`/system-jobs/${selected}`),
  });
  const run = useMutation({
    mutationFn: (jobKey: string) => api(`/system-jobs/${jobKey}/run`, { method: "POST" }),
    onSuccess: async () => {
      setConfirm(null);
      await client.invalidateQueries({ queryKey: ["system-jobs"] });
      await client.invalidateQueries({ queryKey: ["background-jobs"] });
    },
  });
  const enable = useMutation({
    mutationFn: (jobKey: string) => api(`/system-jobs/${jobKey}/enable`, { method: "POST" }),
    onSuccess: async () => client.invalidateQueries({ queryKey: ["system-jobs"] }),
  });
  const disable = useMutation({
    mutationFn: (jobKey: string) => api(`/system-jobs/${jobKey}/disable`, { method: "POST" }),
    onSuccess: async () => {
      setConfirm(null);
      await client.invalidateQueries({ queryKey: ["system-jobs"] });
    },
  });
  const denied = list.error instanceof ApiError && list.error.status === 403;
  const actions = systemJobActions(details.data?.isEnabled ?? false, !manageDenied);
  const actionError = run.error instanceof ApiError ? run.error : enable.error instanceof ApiError ? enable.error : disable.error instanceof ApiError ? disable.error : null;

  function fail(error: unknown) {
    if (error instanceof ApiError && error.status === 403) {
      setManageDenied(true);
    }
    setConfirm(null);
  }

  return (
    <PageState error={denied ? "You do not have permission to view system jobs." : list.error instanceof ApiError ? list.error.message : null} loading={list.isLoading} onRetry={() => void list.refetch()}>
      <div className="mt-4 hidden md:block">
        <DataTable
          columns={[
            { header: "Job Name", cell: (row) => <button className="min-h-11 text-left font-semibold" onClick={() => setSelected(row.jobKey)} type="button">{row.name}</button> },
            { header: "Job Type", cell: (row) => row.jobType },
            { header: "Schedule", cell: (row) => row.schedule },
            { header: "Queue", cell: (row) => row.queue },
            { header: "Enabled", cell: (row) => row.isEnabled ? "Yes" : "No" },
            { header: "Last Run", cell: (row) => formatWhen(row.lastRunUtc) },
            { header: "Next Run", cell: (row) => formatWhen(row.nextRunUtc) },
            { header: "Last Status", cell: (row) => row.lastStatus || "—" },
            { header: "Actions", cell: (row) => <button className="min-h-11" onClick={() => setSelected(row.jobKey)} type="button">Details</button> },
          ]}
          empty="No system jobs are defined."
          rowKey={(row) => row.jobKey}
          rows={list.data ?? []}
        />
      </div>
      <ScrollableGrid className="mt-4 md:hidden">
      <div className="grid w-max min-w-full gap-3">
        {(list.data ?? []).map((job) => (
          <article className="rounded-2xl border border-line p-4" key={job.jobKey}>
            <button className="min-h-11 font-semibold" onClick={() => setSelected(job.jobKey)} type="button">{job.name}</button>
            <p className="text-sm text-muted">{job.schedule} · {job.queue} · {job.isEnabled ? "Enabled" : "Disabled"}</p>
          </article>
        ))}
      </div>
      </ScrollableGrid>
      {details.data ? (
        <section className="mt-4 rounded-2xl border border-line bg-surface p-4">
          <div className="flex items-start justify-between gap-3">
            <h2 className="text-xl font-semibold">{details.data.name}</h2>
            <button className="min-h-11" onClick={() => setSelected(null)} type="button">Close</button>
          </div>
          <dl className="mt-3 grid gap-2 text-sm sm:grid-cols-2">
            <Detail label="Job Key" value={details.data.jobKey} />
            <Detail label="Job Type" value={details.data.jobType} />
            <Detail label="Description" value={details.data.description || "—"} />
            <Detail label="Schedule" value={details.data.schedule} />
            <Detail label="Queue" value={details.data.queue} />
            <Detail label="Enabled" value={details.data.isEnabled ? "Yes" : "No"} />
            <Detail label="Last Run" value={formatWhen(details.data.lastRunUtc)} />
            <Detail label="Last Status" value={details.data.lastStatus || "—"} />
            <Detail label="Last Duration" value={formatDuration(details.data.lastDurationSeconds)} />
            <Detail label="Next Run" value={formatWhen(details.data.nextRunUtc)} />
          </dl>
          {details.data.lastError ? <p className="mt-3 text-sm text-accent" role="alert">{details.data.lastError}</p> : null}
          {details.data.statistics ? (
            <dl className="mt-4 grid gap-2 text-sm sm:grid-cols-3">
              <Detail label="Members processed" value={String(details.data.statistics.membersProcessed)} />
              <Detail label="10-day notifications" value={String(details.data.statistics.tenDay)} />
              <Detail label="5-day notifications" value={String(details.data.statistics.fiveDay)} />
              <Detail label="Expiry-day notifications" value={String(details.data.statistics.today)} />
              <Detail label="Skipped" value={String(details.data.statistics.skipped)} />
              <Detail label="Failed" value={String(details.data.statistics.failed)} />
            </dl>
          ) : null}
          {actionError ? <p className="mt-3 text-sm text-accent" role="alert">{actionError.status === 403 ? "You do not have permission to manage system jobs." : actionError.message}</p> : null}
          <div className="mt-4 flex flex-wrap gap-3">
            {actions.enable ? <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" disabled={enable.isPending} onClick={() => enable.mutate(details.data!.jobKey, { onError: fail })} type="button">Enable</button> : null}
            {actions.disable ? <button className="min-h-11" onClick={() => setConfirm("disable")} type="button">Disable</button> : null}
            {actions.run ? <button className="min-h-11" onClick={() => setConfirm("run")} type="button">Run now</button> : null}
          </div>
        </section>
      ) : null}
      {confirm && selected ? (
        <ConfirmDialog
          body={confirm === "run" ? "Run this system job now? Reminders that were already generated will not be sent again." : "Disable this system job? The daily schedule will stop and no new reminders will be generated."}
          confirmLabel={confirm === "run" ? "Run now" : "Disable"}
          onCancel={() => setConfirm(null)}
          onConfirm={() => {
            const action = confirm === "run" ? run : disable;
            action.mutate(selected, { onError: fail });
          }}
          pending={run.isPending || disable.isPending}
          title={confirm === "run" ? "Run system job" : "Disable system job"}
        />
      ) : null}
    </PageState>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-muted">{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}
