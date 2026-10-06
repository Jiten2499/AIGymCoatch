"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { Page, TenantLicense } from "../../../lib/types";

export default function AssignmentsPage() {
  const licenses = useQuery({ queryKey: ["licenses"], queryFn: () => api<Page<TenantLicense>>("/platform/licenses?page=1&pageSize=100") });

  return (
    <PageState error={licenses.error instanceof ApiError ? licenses.error.message : null} loading={licenses.isLoading} onRetry={() => void licenses.refetch()}>
      <h1 className="text-3xl font-semibold">Subscription seats</h1>
      <p className="mt-2 max-w-3xl text-muted">Seats belong to a workspace subscription. Open a workspace to assign or release a member. The API rejects a user who is not a member of that workspace, and a full pool returns a conflict.</p>
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Workspace", value: (row) => row.tenantName, cell: (row) => <Link href={`/admin/licenses/${row.licenseId}`}>{row.tenantName}</Link> },
            { header: "Seats", cell: (row) => String(row.baseCapacity) },
            { header: "Assigned", cell: (row) => String(row.assigned) },
            { header: "Available", cell: (row) => String(row.available) },
            { header: "Usable", cell: (row) => (row.usable ? "Yes" : "No") },
          ]}
          empty="No subscriptions were returned."
          rowKey={(row) => row.licenseId}
          rows={licenses.data?.items ?? []}
        />
      </div>
    </PageState>
  );
}
