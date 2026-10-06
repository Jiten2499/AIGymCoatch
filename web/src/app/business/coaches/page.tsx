"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import { getSession, subscribeSession } from "../../../lib/session";
import type { Coach, CreatedCoach, Page } from "../../../lib/types";
import { AddCoachDialog, EditCoachDialog, PasswordDialog } from "./CoachDialogs";

export default function BusinessCoachesPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const coaches = useQuery({
    queryKey: ["coaches", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<Coach>>("/trainers?page=1&pageSize=100"),
  });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState(false);
  const [issued, setIssued] = useState<CreatedCoach | null>(null);
  const rows = coaches.data?.items ?? [];
  const selectedRows = rows.filter((row) => selected.has(row.id));

  function toggle(row: Coach) {
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

  function toggleVisible(visible: Coach[]) {
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
    <PageState error={coaches.error instanceof ApiError ? coaches.error.message : null} loading={coaches.isLoading} onRetry={() => void coaches.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Coaches</h1>
          <p className="mt-2 max-w-3xl text-muted">Trainers who work in this workspace. {coaches.data ? `${coaches.data.totalCount} total.` : ""}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAdding(true)} type="button">Add coach</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setEditing(true)} type="button">Edit</button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => void coaches.refetch()} type="button">{coaches.isFetching ? "Refreshing..." : "Refresh"}</button>
        </div>
      </div>
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Name", cell: (row) => `${row.firstName} ${row.lastName}` },
            { header: "Email", cell: (row) => row.email },
            { header: "Specialization", cell: (row) => row.specialization || "—" },
            { header: "Experience", cell: (row) => row.experienceYears === null ? "—" : `${row.experienceYears} years` },
            { header: "Certification", cell: (row) => row.certification || "—" },
            { header: "Status", cell: (row) => row.status },
          ]}
          empty="No coaches were returned."
          rowKey={(row) => row.id}
          rows={rows}
          selection={{ isSelected: (row) => selected.has(row.id), label: (row) => `Select ${row.firstName} ${row.lastName}`, onToggle: toggle, onToggleVisible: toggleVisible }}
        />
      </div>
      {adding ? (
        <AddCoachDialog
          onCancel={() => setAdding(false)}
          onCreated={async (created) => {
            setAdding(false);
            setIssued(created);
            await client.invalidateQueries({ queryKey: ["coaches", tenantId] });
          }}
        />
      ) : null}
      {editing && selectedRows[0] ? (
        <EditCoachDialog
          coach={selectedRows[0]}
          onCancel={() => setEditing(false)}
          onSaved={async () => {
            setEditing(false);
            await client.invalidateQueries({ queryKey: ["coaches", tenantId] });
          }}
        />
      ) : null}
      {issued ? <PasswordDialog created={issued} onClose={() => setIssued(null)} /> : null}
    </PageState>
  );
}
