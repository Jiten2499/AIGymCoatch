import { describe, expect, it } from "vitest";
import { canCancel, canRetry, formatDuration, jobListPath, jobRefreshInterval, nextSort, summaryPath, type JobListFilters } from "./backgroundJobs";

const filters: JobListFilters = {
  term: " Report ",
  jobType: "Reports.Generate",
  status: "Failed",
  queue: "default",
  tenantId: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1",
  from: "2026-10-01",
  to: "2026-10-08",
  correlationId: "corr-1",
  page: 2,
  pageSize: 10,
  sort: "createdAt",
  descending: true,
};

describe("background job queries", () => {
  it("sends list filters to the server", () => {
    const path = jobListPath(filters);
    expect(path).toContain("page=2");
    expect(path).toContain("pageSize=10");
    expect(path).toContain("jobType=Reports.Generate");
    expect(path).toContain("status=Failed");
    expect(path).toContain("queue=default");
    expect(path).toContain("tenantId=aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    expect(path).toContain("correlationId=corr-1");
    expect(path).toContain("term=Report");
    expect(path).toContain("sort=createdAt");
    expect(path).toContain("descending=true");
    expect(path.startsWith("/background-jobs?")).toBe(true);
  });

  it("ignores a tenant id that is not a guid", () => {
    expect(jobListPath({ ...filters, tenantId: "abc fitness" })).not.toContain("tenantId=");
    expect(summaryPath("not-a-tenant")).toBe("/background-jobs/summary");
  });

  it("polls active work often and pauses while hidden", () => {
    expect(jobRefreshInterval({ queued: 1, scheduled: 0, processing: 0, failed: 0 }, true)).toBe(5000);
    expect(jobRefreshInterval({ queued: 0, scheduled: 0, processing: 0, failed: 2 }, true)).toBe(5000);
    expect(jobRefreshInterval({ queued: 0, scheduled: 0, processing: 0, failed: 0 }, true)).toBe(60000);
    expect(jobRefreshInterval({ queued: 4, scheduled: 0, processing: 1, failed: 1 }, false)).toBe(false);
  });

  it("allows retry only for failed jobs and cancel only while the job is still active", () => {
    expect(canRetry("Failed")).toBe(true);
    expect(canRetry("Succeeded")).toBe(false);
    expect(canCancel("Processing")).toBe(true);
    expect(canCancel("Scheduled")).toBe(true);
    expect(canCancel("Succeeded")).toBe(false);
    expect(canCancel("Failed")).toBe(false);
  });

  it("formats duration and toggles the server sort", () => {
    expect(formatDuration(null)).toBe("—");
    expect(formatDuration(12.2)).toBe("12s");
    expect(formatDuration(75)).toBe("1m 15s");
    expect(nextSort("createdAt", true, "Status")).toEqual({ sort: "status", descending: true });
    expect(nextSort("status", true, "Status")).toEqual({ sort: "status", descending: false });
  });
});
