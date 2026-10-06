"use client";

import { useQuery } from "@tanstack/react-query";
import { useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import { getSession, subscribeSession } from "../../../lib/session";
import type { WorkspacePlan } from "../../../lib/types";

export default function BusinessPlansPage() {
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const plans = useQuery({
    queryKey: ["workspace-plans", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<WorkspacePlan[]>("/business/plans"),
  });
  const rows = plans.data ?? [];

  return (
    <PageState error={plans.error instanceof ApiError ? plans.error.message : null} loading={plans.isLoading} onRetry={() => void plans.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Plans</h1>
          <p className="mt-2 max-w-3xl text-muted">Subscriptions assigned to this workspace. Seat counts are the licenses you can give to users.</p>
        </div>
        <button className="min-h-11 rounded-full border border-line px-4" onClick={() => void plans.refetch()} type="button">{plans.isFetching ? "Refreshing..." : "Refresh"}</button>
      </div>
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Plan", cell: (row) => row.planName },
            { header: "Code", cell: (row) => row.planCode },
            { header: "Status", cell: (row) => row.usable ? "Usable" : row.status },
            { header: "Starts", value: (row) => row.startsAt, cell: (row) => new Date(row.startsAt).toLocaleDateString() },
            { header: "Expires", value: (row) => row.expiresAt, cell: (row) => row.expiresAt ? new Date(row.expiresAt).toLocaleDateString() : "No expiry" },
            { header: "Seats", value: (row) => row.seats, cell: (row) => String(row.seats) },
            { header: "Assigned", value: (row) => row.assigned, cell: (row) => String(row.assigned) },
            { header: "Available", value: (row) => row.available, cell: (row) => String(row.available) },
            { header: "Features", cell: (row) => row.features.filter((feature) => feature.enabled).map((feature) => feature.code).join(", ") || "None" },
          ]}
          empty="No plans are assigned to this workspace."
          rowKey={(row) => row.subscriptionId}
          rows={rows}
        />
      </div>
    </PageState>
  );
}
