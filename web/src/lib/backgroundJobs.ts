export const jobStatuses = ["Queued", "Scheduled", "Processing", "Succeeded", "Failed", "Deleted", "Awaiting"] as const;

export type JobStatus = (typeof jobStatuses)[number];

export type BackgroundJob = {
  jobId: string;
  jobType: string;
  queue: string;
  status: JobStatus;
  tenantId: string | null;
  tenantName: string | null;
  correlationId: string | null;
  createdAt: string;
  scheduledAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  durationSeconds: number | null;
  attemptCount: number;
  errorMessage: string | null;
  createdByUserId: string | null;
  sourceType: string | null;
  sourceId: string | null;
};

export type BackgroundJobHistory = { status: JobStatus; timestamp: string; reason: string | null };

export type BackgroundJobDetails = { job: BackgroundJob; history: BackgroundJobHistory[] };

export type BackgroundJobSummary = {
  queued: number;
  scheduled: number;
  processing: number;
  succeeded: number;
  failed: number;
  jobsToday: number;
  jobsLast24Hours: number;
  averageDurationSeconds: number | null;
  queues: string[];
};

export type JobListFilters = {
  term: string;
  jobType: string;
  status: string;
  queue: string;
  tenantId: string;
  from: string;
  to: string;
  correlationId: string;
  page: number;
  pageSize: number;
  sort: "createdAt" | "status";
  descending: boolean;
};

export const statusMeaning: Record<JobStatus, string> = {
  Queued: "Waiting to execute",
  Processing: "Currently executing",
  Scheduled: "Waiting for scheduled time",
  Succeeded: "Completed successfully",
  Failed: "Execution failed",
  Deleted: "No longer active",
  Awaiting: "Waiting on another job",
};

export function jobListPath(filters: JobListFilters): string {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
    sort: filters.sort,
    descending: String(filters.descending),
  });
  set(params, "term", filters.term);
  set(params, "jobType", filters.jobType);
  set(params, "status", filters.status);
  set(params, "queue", filters.queue);
  set(params, "correlationId", filters.correlationId);
  if (isGuid(filters.tenantId)) {
    params.set("tenantId", filters.tenantId);
  }
  if (filters.from) {
    params.set("from", new Date(filters.from).toISOString());
  }
  if (filters.to) {
    params.set("to", new Date(`${filters.to}T23:59:59`).toISOString());
  }
  return `/background-jobs?${params}`;
}

export function summaryPath(tenantId: string): string {
  return isGuid(tenantId) ? `/background-jobs/summary?tenantId=${tenantId}` : "/background-jobs/summary";
}

export function canRetry(status: JobStatus): boolean {
  return status === "Failed";
}

export function canCancel(status: JobStatus): boolean {
  return status === "Queued" || status === "Scheduled" || status === "Processing" || status === "Awaiting";
}

export function formatDuration(seconds: number | null): string {
  if (seconds === null || Number.isNaN(seconds)) {
    return "—";
  }
  const rounded = Math.max(0, Math.round(seconds));
  const minutes = Math.floor(rounded / 60);
  const rest = rounded % 60;
  return minutes === 0 ? `${rest}s` : `${minutes}m ${rest}s`;
}

export function formatWhen(value: string | null): string {
  return value ? new Date(value).toLocaleString() : "—";
}

export function jobRefreshInterval(summary: Pick<BackgroundJobSummary, "queued" | "scheduled" | "processing" | "failed"> | undefined, visible: boolean): number | false {
  if (!visible) {
    return false;
  }
  if (!summary) {
    return 15000;
  }
  const active = summary.queued + summary.scheduled + summary.processing + summary.failed;
  return active > 0 ? 5000 : 60000;
}

export function nextSort(current: JobListFilters["sort"], descending: boolean, header: string): { sort: JobListFilters["sort"]; descending: boolean } {
  const sort = header === "Status" ? "status" : "createdAt";
  if (current === sort) {
    return { sort, descending: !descending };
  }
  return { sort, descending: true };
}

function set(params: URLSearchParams, key: string, value: string) {
  const trimmed = value.trim();
  if (trimmed) {
    params.set(key, trimmed);
  }
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim());
}
