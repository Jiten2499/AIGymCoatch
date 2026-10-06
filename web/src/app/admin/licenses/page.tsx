"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { PageState } from "../../../components/AdminShell";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { Page, TenantLicense } from "../../../lib/types";

export default function LicensesPage() {
  const client = useQueryClient();
  const [revoke, setRevoke] = useState<TenantLicense | null>(null);
  const licenses = useQuery({ queryKey: ["licenses"], queryFn: () => api<Page<TenantLicense>>("/platform/licenses?page=1&pageSize=100") });
  const revokeLicense = useMutation({
    mutationFn: (id: string) => api(`/platform/licenses/${id}/revoke`, { method: "POST" }),
    onSuccess: async () => {
      setRevoke(null);
      await client.invalidateQueries({ queryKey: ["licenses"] });
    },
  });
  return (
    <PageState error={licenses.error instanceof ApiError ? licenses.error.message : null} loading={licenses.isLoading} onRetry={() => void licenses.refetch()}>
      <h1 className="text-3xl font-semibold">Subscriptions</h1>
      <p className="mt-2 text-muted">A workspace receives its plan subscription when it is created. This screen extends or cancels an existing subscription. Entitlements come from the plan, not from per-workspace copies.</p>
      {revokeLicense.error instanceof ApiError ? <p className="mt-4" role="alert">{revokeLicense.error.message}</p> : null}
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Workspace", value: (row) => row.tenantName, cell: (row) => <Link href={`/admin/licenses/${row.licenseId}`}>{row.tenantName}</Link> },
            { header: "Plan", cell: (row) => row.planCode },
            { header: "Status", cell: (row) => row.usable ? "Usable" : row.status },
            { header: "Expires", cell: (row) => row.expiresAt ? new Date(row.expiresAt).toLocaleDateString() : "None" },
            { header: "Seats", cell: (row) => `${row.assigned} assigned · ${row.available} available` },
            { header: "Revoke", cell: (row) => <button className="min-h-11" onClick={() => setRevoke(row)} type="button">Revoke</button> },
          ]}
          empty="No subscriptions were returned."
          rowKey={(row) => row.licenseId}
          rows={licenses.data?.items ?? []}
        />
      </div>
      {revoke ? <ConfirmDialog body={`Cancel the ${revoke.planCode} subscription for ${revoke.tenantName}?`} confirmLabel="Cancel subscription" onCancel={() => setRevoke(null)} onConfirm={() => revokeLicense.mutate(revoke.licenseId)} pending={revokeLicense.isPending} title="Cancel subscription" /> : null}
    </PageState>
  );
}
