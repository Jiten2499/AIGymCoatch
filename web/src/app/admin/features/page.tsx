"use client";

import { useQuery } from "@tanstack/react-query";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { FeatureRow } from "../../../lib/types";

export default function FeaturesPage() {
  const features = useQuery({ queryKey: ["features"], queryFn: () => api<FeatureRow[]>("/platform/features") });
  return (
    <PageState error={features.error instanceof ApiError ? features.error.message : null} loading={features.isLoading} onRetry={() => void features.refetch()}>
      <h1 className="text-3xl font-semibold">Features</h1>
      <p className="mt-2 text-muted">Codes come from the feature catalog. Enable them on a plan. This page does not create new feature keys.</p>
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Code", cell: (row) => row.code },
            { header: "Name", cell: (row) => row.name },
            { header: "Category", cell: (row) => row.category },
            { header: "Scope", cell: (row) => row.scope },
            { header: "Active", cell: (row) => (row.isActive ? "Active" : "Inactive") },
          ]}
          empty="No feature definitions are available."
          rowKey={(row) => row.code}
          rows={features.data ?? []}
        />
      </div>
    </PageState>
  );
}
