"use client";

import { useQuery } from "@tanstack/react-query";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { AuditEntry, Page } from "../../../lib/types";

export default function AuditPage() {
  const audit = useQuery({ queryKey: ["audit"], queryFn: () => api<Page<AuditEntry>>("/platform/audit?page=1&pageSize=100") });
  return (
    <PageState error={audit.error instanceof ApiError ? audit.error.message : null} loading={audit.isLoading} onRetry={() => void audit.refetch()}>
      <h1 className="text-3xl font-semibold">Audit</h1>
      <p className="mt-2 text-muted">Entries show the action and target. Payloads, keys, and tokens are not included.</p>
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "When", value: (row) => row.createdAt, cell: (row) => new Date(row.createdAt).toLocaleString() },
            { header: "Action", cell: (row) => row.action },
            { header: "Entity", cell: (row) => row.entity },
            { header: "Target", cell: (row) => row.entityId ?? "" },
          ]}
          empty="No audit entries were returned."
          rowKey={(row) => row.id}
          rows={audit.data?.items ?? []}
        />
      </div>
    </PageState>
  );
}
