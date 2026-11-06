"use client";

import type { ReactNode } from "react";
import { canCancel, canRetry, formatDuration, formatWhen, statusMeaning, type BackgroundJobDetails, type JobStatus } from "../../../lib/backgroundJobs";

export function JobDetails({
  details,
  manageDenied,
  pending,
  error,
  onClose,
  onRetry,
  onCancel,
}: {
  details: BackgroundJobDetails;
  manageDenied: boolean;
  pending: boolean;
  error: string | null;
  onClose: () => void;
  onRetry: () => void;
  onCancel: () => void;
}) {
  const job = details.job;
  const failure = [...details.history].reverse().find((entry) => entry.status === "Failed");
  return (
    <aside aria-label="Job Details" className="fixed inset-y-0 right-0 z-20 w-full max-w-xl overflow-y-auto border-l border-line bg-background p-6">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-sm text-muted">Job Details</p>
          <h2 className="mt-1 text-2xl font-semibold">{job.jobType || "Unknown job"}</h2>
        </div>
        <button className="min-h-11 rounded-full border border-line px-4" onClick={onClose} type="button">Close</button>
      </div>
      <StatusBadge status={job.status} />
      <p className="mt-2 text-sm text-muted">{statusMeaning[job.status]}</p>
      <Section title="General">
        <Field label="Job ID" value={job.jobId} />
        <Field label="Job Type" value={job.jobType || "Unknown job"} />
        <Field label="Queue" value={job.queue} />
        <Field label="Status" value={job.status} />
        <Field label="Tenant" value={job.tenantName || job.tenantId || "—"} />
        <Field label="Correlation ID" value={job.correlationId || "—"} />
      </Section>
      <Section title="Timing">
        <Field label="Created" value={formatWhen(job.createdAt)} />
        <Field label="Scheduled" value={formatWhen(job.scheduledAt)} />
        <Field label="Started" value={formatWhen(job.startedAt)} />
        <Field label="Completed" value={formatWhen(job.completedAt)} />
        <Field label="Duration" value={formatDuration(job.durationSeconds)} />
      </Section>
      <Section title="Execution">
        <Field label="Attempt count" value={String(job.attemptCount)} />
        <Field label="Current state" value={job.status} />
        <Field label="Source type" value={job.sourceType || "—"} />
        <Field label="Source ID" value={job.sourceId || "—"} />
      </Section>
      <Section title="Error">
        <Field label="Error message" value={job.errorMessage || "None"} />
        <Field label="Failure timestamp" value={formatWhen(failure?.timestamp ?? null)} />
        <Field label="Retry information" value={canRetry(job.status) ? "This failed job can be retried." : "Retry is available after a job fails."} />
      </Section>
      <Section title="History">
        {details.history.length === 0 ? <p className="text-muted">No state history was returned.</p> : (
          <ol className="grid gap-2">
            {details.history.map((entry, index) => (
              <li className="rounded-xl border border-line px-3 py-2" key={`${entry.timestamp}-${entry.status}-${index}`}>
                <p className="font-medium">{entry.status}</p>
                <p className="text-sm text-muted">{formatWhen(entry.timestamp)}{entry.reason ? ` · ${entry.reason}` : ""}</p>
              </li>
            ))}
          </ol>
        )}
      </Section>
      {error ? <p className="mt-4" role="alert">{error}</p> : null}
      {manageDenied ? <p className="mt-4 text-muted">You can view this job. Retry and cancel require manage permission.</p> : (
        <div className="mt-6 flex flex-wrap gap-3">
          {canRetry(job.status) ? <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" disabled={pending} onClick={onRetry} type="button">Retry</button> : null}
          {canCancel(job.status) ? <button className="min-h-11 rounded-full border border-line px-4" disabled={pending} onClick={onCancel} type="button">Cancel</button> : null}
        </div>
      )}
    </aside>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return <section className="mt-6"><h3 className="font-semibold">{title}</h3><div className="mt-3 grid gap-2">{children}</div></section>;
}

function Field({ label, value }: { label: string; value: string }) {
  return <p><span className="text-muted">{label}: </span>{value}</p>;
}

export function StatusBadge({ status }: { status: JobStatus }) {
  const failed = status === "Failed";
  return <span className={`mt-3 inline-flex rounded-full border px-3 py-1 text-sm ${failed ? "border-accent font-semibold text-accent" : "border-line"}`}>{status}</span>;
}
