"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { Page, PlatformUser } from "../../../lib/types";
import { AssignCatalogPlanDialog } from "../AssignPlanDialog";

export default function UsersPage() {
  const client = useQueryClient();
  const users = useQuery({ queryKey: ["platform-users"], queryFn: () => api<Page<PlatformUser>>("/platform/users?page=1&pageSize=100") });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [assigning, setAssigning] = useState(false);
  const rows = users.data?.items ?? [];
  const selectedRows = rows.filter((row) => selected.has(row.id));

  function toggle(row: PlatformUser) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(row.id)) {
        next.delete(row.id);
      } else {
        next.add(row.id);
      }
      return next;
    });
  }

  function toggleVisible(visible: PlatformUser[]) {
    setSelected((current) => {
      const next = new Set(current);
      const ids = visible.map((row) => row.id);
      if (ids.every((id) => next.has(id))) {
        ids.forEach((id) => next.delete(id));
      } else {
        ids.forEach((id) => next.add(id));
      }
      return next;
    });
  }

  return (
    <PageState error={users.error instanceof ApiError ? users.error.message : null} loading={users.isLoading} onRetry={() => void users.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Users</h1>
          <p className="mt-2 max-w-3xl text-muted">Select one account, then Assign plan, to give that person a personal plan. A business workspace assigns seats to its own members from Business, then Users. {users.data ? `${users.data.totalCount} total.` : ""}</p>
        </div>
        <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setAssigning(true)} type="button">Assign plan</button>
      </div>
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Name", cell: (row) => `${row.firstName} ${row.lastName}` },
            { header: "Email", cell: (row) => row.email },
            { header: "Plan", cell: (row) => row.plan || "—" },
            { header: "Status", cell: (row) => row.status },
            { header: "Personal workspace", cell: (row) => row.personalTenantName || row.personalTenantId },
            { header: "Created", value: (row) => row.createdAt, cell: (row) => new Date(row.createdAt).toLocaleDateString() },
          ]}
          empty="No users were returned."
          rowKey={(row) => row.id}
          rows={rows}
          selection={{ isSelected: (row) => selected.has(row.id), label: (row) => `Select ${row.firstName} ${row.lastName}`, onToggle: toggle, onToggleVisible: toggleVisible }}
        />
      </div>
      {assigning && selectedRows[0] ? (
        <AssignCatalogPlanDialog
          audience="personal"
          description={`Choose the personal plan for ${selectedRows[0].firstName} ${selectedRows[0].lastName}.`}
          onCancel={() => setAssigning(false)}
          onSaved={async () => {
            setAssigning(false);
            await client.invalidateQueries({ queryKey: ["platform-users"] });
          }}
          path={`/platform/users/${selectedRows[0].id}/plan`}
          title="Assign plan"
        />
      ) : null}
    </PageState>
  );
}
