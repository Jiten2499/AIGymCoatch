"use client";

import { useQuery } from "@tanstack/react-query";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { ExpiringLicense } from "../../../lib/types";

export default function ExpiringPage() {
  const licenses = useQuery({ queryKey: ["expiring"], queryFn: () => api<ExpiringLicense[]>("/platform/licenses/expiring?withinDays=30") });
  return (
    <PageState error={licenses.error instanceof ApiError ? licenses.error.message : null} loading={licenses.isLoading} onRetry={() => void licenses.refetch()}>
      <h1 className="text-3xl font-semibold">Expiring licenses</h1>
      <p className="mt-2 text-muted">The API returns active licenses with an expiry inside 30 days. Usability is still decided by the license status on the server.</p>
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Workspace", cell: (row) => row.tenantId },
            { header: "Plan", cell: (row) => row.planCode },
            { header: "Expires", cell: (row) => row.expiresAt ? new Date(row.expiresAt).toLocaleString() : "None" },
          ]}
          empty="No licenses expire in this window."
          rowKey={(row) => row.licenseId}
          rows={licenses.data ?? []}
        />
      </div>
    </PageState>
  );
}
