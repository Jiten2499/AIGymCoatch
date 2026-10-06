"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { LlmConnection } from "../../../lib/types";

export default function ModelsPage() {
  const connections = useQuery({ queryKey: ["llm"], queryFn: () => api<LlmConnection[]>("/llm/connections") });

  return (
    <PageState error={connections.error instanceof ApiError ? connections.error.message : null} loading={connections.isLoading} onRetry={() => void connections.refetch()}>
      <h1 className="text-3xl font-semibold">Models</h1>
      <p className="mt-2 max-w-3xl text-muted">Each connection stores its own models. Refreshing a provider lists model ids and does not save them. Chat is the only capability the provider executes.</p>
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Connection", value: (row) => row.name, cell: (row) => <Link href={`/admin/llm/${row.id}`}>{row.name}</Link> },
            { header: "Default model", cell: (row) => row.defaultModel ?? "None" },
            { header: "Saved models", cell: (row) => String(row.modelCount) },
            { header: "Health", cell: (row) => row.healthStatus },
          ]}
          empty="Add a connection before saving models."
          rowKey={(row) => row.id}
          rows={connections.data ?? []}
        />
      </div>
    </PageState>
  );
}
