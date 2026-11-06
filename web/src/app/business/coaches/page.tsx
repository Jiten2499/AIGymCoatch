"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { ProfileDeleteDialog } from "../../../components/ProfileDeleteDialog";
import { api, ApiError } from "../../../lib/api";
import { getSession, subscribeSession } from "../../../lib/session";
import type { Coach, CreatedCoach, Page } from "../../../lib/types";
import { TrainerPerformancePanel } from "../TrainerPerformancePanel";
import { CoachDetails } from "./CoachDetails";
import { AddCoachDialog, EditCoachDialog, PasswordDialog } from "./CoachDialogs";

export default function BusinessCoachesPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [specialization, setSpecialization] = useState("");
  const [applied, setApplied] = useState({ search: "", status: "", specialization: "" });
  const [viewing, setViewing] = useState<string | null>(null);
  const [showPerformance, setShowPerformance] = useState(false);
  const coaches = useQuery({
    queryKey: ["coaches", tenantId, page, pageSize, applied],
    enabled: tenantId !== null,
    queryFn: () => api<Page<Coach>>(`/trainers?page=${page}&pageSize=${pageSize}&search=${encodeURIComponent(applied.search)}&status=${encodeURIComponent(applied.status)}&specialization=${encodeURIComponent(applied.specialization)}`),
  });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deletedNotice, setDeletedNotice] = useState<string | null>(null);
  const [issued, setIssued] = useState<CreatedCoach | null>(null);
  const [photoNotice, setPhotoNotice] = useState<string | null>(null);
  const rows = coaches.data?.items ?? [];
  const selectedRows = rows.filter((row) => selected.has(row.id));
  const remove = useMutation({
    mutationFn: (id: string) => api(`/trainers/${id}`, { method: "DELETE" }),
    onSuccess: async () => {
      const name = selectedRows[0] ? `${selectedRows[0].firstName} ${selectedRows[0].lastName}` : "The coach";
      setConfirmDelete(false);
      setSelected(new Set());
      setViewing(null);
      setDeletedNotice(`${name} was deleted from active business views. The record is retained as soft-deleted data.`);
      await client.invalidateQueries({ queryKey: ["coaches"] });
      await client.invalidateQueries({ queryKey: ["trainers"] });
      await client.invalidateQueries({ queryKey: ["members"] });
    },
  });

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

  if (showPerformance) {
    return (
      <div className="grid gap-6">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-3xl font-semibold">Performance</h1>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setShowPerformance(false)} type="button">Back to coaches</button>
        </div>
        <TrainerPerformancePanel />
      </div>
    );
  }

  return (
    <PageState error={coaches.error instanceof ApiError ? coaches.error.message : null} loading={coaches.isLoading} onRetry={() => void coaches.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Coaches</h1>
          <p className="mt-2 max-w-3xl text-muted">Coaches who work in this workspace. {coaches.data ? `${coaches.data.totalCount} total.` : ""}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAdding(true)} type="button">Add coach</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setEditing(true)} type="button">Edit</button>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={selectedRows.length !== 1} onClick={() => setConfirmDelete(true)} type="button">Delete</button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setShowPerformance(true)} type="button">Performance</button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => void coaches.refetch()} type="button">{coaches.isFetching ? "Refreshing..." : "Refresh"}</button>
        </div>
      </div>
      {deletedNotice ? <p className="mt-4" role="status">{deletedNotice}</p> : null}
      <div className="mt-4 flex flex-wrap gap-2">
        <input aria-label="Search coaches" className="field max-w-xs" onChange={(event) => setSearch(event.target.value)} placeholder="Name, email, or employee code" value={search} />
        <select aria-label="Status" className="field max-w-xs" onChange={(event) => setStatus(event.target.value)} value={status}>
          <option value="">Any status</option>
          <option value="Active">Active</option>
          <option value="Inactive">Inactive</option>
        </select>
        <input aria-label="Specialization" className="field max-w-xs" onChange={(event) => setSpecialization(event.target.value)} placeholder="Specialization" value={specialization} />
        <button className="min-h-11 rounded-full border border-line px-4" onClick={() => { setPage(1); setApplied({ search, status, specialization }); }} type="button">Apply</button>
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
            { header: "Assigned members", cell: (row) => String(row.assignedMembers ?? 0) },
            { header: "Actions", cell: (row) => (
              <span className="flex gap-3">
                <button className="min-h-11" onClick={() => setViewing(row.id)} type="button">View</button>
                <button className="min-h-11" onClick={() => { setSelected(new Set([row.id])); setConfirmDelete(true); }} type="button">Delete</button>
              </span>
            ) },
          ]}
          empty="No coaches were returned."
          paging={{ page, pageSize, totalCount: coaches.data?.totalCount ?? 0, onPageChange: setPage, onPageSizeChange: (size) => { setPageSize(size); setPage(1); } }}
          rowKey={(row) => row.id}
          rows={rows}
          selection={{ isSelected: (row) => selected.has(row.id), label: (row) => `Select ${row.firstName} ${row.lastName}`, onToggle: toggle, onToggleVisible: toggleVisible }}
        />
      </div>
      {adding ? (
        <AddCoachDialog
          onCancel={() => setAdding(false)}
          onCreated={async (created, photoWarning) => {
            setAdding(false);
            setIssued(created);
            setPhotoNotice(photoWarning ?? null);
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
      {issued ? <PasswordDialog created={issued} notice={photoNotice} onClose={() => { setIssued(null); setPhotoNotice(null); }} /> : null}
      {viewing ? <CoachDetails onClose={() => setViewing(null)} onDeleted={() => { setViewing(null); setDeletedNotice("The coach was deleted from active business views. The record is retained as soft-deleted data."); void client.invalidateQueries({ queryKey: ["coaches"] }); void client.invalidateQueries({ queryKey: ["trainers"] }); void client.invalidateQueries({ queryKey: ["members"] }); }} trainerId={viewing} /> : null}
      {confirmDelete && selectedRows.length > 0 ? (
        <ProfileDeleteDialog
          error={remove.error instanceof ApiError ? remove.error.message : null}
          name={selectedRows.map((row) => `${row.firstName} ${row.lastName}`).join(", ")}
          onCancel={() => { setConfirmDelete(false); remove.reset(); }}
          onConfirm={() => selectedRows[0] ? remove.mutate(selectedRows[0].id) : undefined}
          pending={remove.isPending}
          title="Delete coach"
        />
      ) : null}
    </PageState>
  );
}
