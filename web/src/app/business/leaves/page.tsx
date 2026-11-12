"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useMemo, useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { api, ApiError } from "../../../lib/api";
import {
  cancelStaffLeave,
  coversDay,
  getStaffLeave,
  listMyStaffLeaves,
  listStaffLeaves,
  monthBounds,
  overlapsRange,
  type LeaveView,
} from "../../../lib/leaves";
import { getSession, subscribeSession } from "../../../lib/session";
import type { Page } from "../../../lib/types";
import { ApplyLeaveDialog } from "./ApplyLeaveDialog";
import { LeaveDetailsAside } from "./LeaveDetailsAside";
import { StaffLeaveCalendar } from "./StaffLeaveCalendar";

type StaffOption = { id: string; employeeCode: string; firstName: string; lastName: string; status: string };

export default function StaffLeavesPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const initial = monthBounds(new Date().getFullYear(), new Date().getMonth() + 1);
  const [range, setRange] = useState(initial);
  const [view, setView] = useState<LeaveView>("month");
  const [status, setStatus] = useState("");
  const [leaveType, setLeaveType] = useState("");
  const [staffProfileId, setStaffProfileId] = useState("");
  const [staffSearch, setStaffSearch] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [applying, setApplying] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  const access = useQuery({
    queryKey: ["me-access", tenantId, "leaves"],
    enabled: tenantId !== null,
    queryFn: () => api<{ permissions: string[] }>("/users/me/access"),
  });
  const permissions = useMemo(() => new Set(access.data?.permissions ?? []), [access.data?.permissions]);
  const canReadOthers = permissions.has("LEAVE_READ");
  const canApply = permissions.has("LEAVE_APPLY");

  const staff = useQuery({
    queryKey: ["staff", tenantId, "leaves-filter"],
    enabled: tenantId !== null && canReadOthers,
    queryFn: () => api<Page<StaffOption>>("/staff?page=1&pageSize=200"),
    retry: false,
  });

  const leaves = useQuery({
    queryKey: ["staff-leaves", tenantId, canReadOthers, range.from, range.to, status, leaveType, staffProfileId],
    enabled: tenantId !== null && access.isSuccess && (canReadOthers || canApply),
    queryFn: async () => {
      if (canReadOthers) {
        return listStaffLeaves(range.from, range.to, staffProfileId || undefined, leaveType || undefined, status || undefined);
      }
      const mine = await listMyStaffLeaves();
      return mine.filter((item) => overlapsRange(item, range.from, range.to)
        && (!status || item.status === status)
        && (!leaveType || item.leaveType === leaveType));
    },
  });

  const detail = useQuery({
    queryKey: ["staff-leave", tenantId, selectedId],
    enabled: tenantId !== null && selectedId !== null,
    queryFn: () => getStaffLeave(selectedId!),
  });

  const cancel = useMutation({
    mutationFn: (id: string) => cancelStaffLeave(id),
    onSuccess: async () => {
      setNotice("Leave request cancelled.");
      setSelectedId(null);
      await client.invalidateQueries({ queryKey: ["staff-leaves", tenantId] });
      await client.invalidateQueries({ queryKey: ["staff-leave", tenantId] });
    },
  });

  const staffOptions = useMemo(() => {
    const rows = staff.data?.items ?? [];
    const term = staffSearch.trim().toLowerCase();
    if (!term) return rows;
    return rows.filter((row) => `${row.firstName} ${row.lastName} ${row.employeeCode}`.toLowerCase().includes(term));
  }, [staff.data?.items, staffSearch]);

  const items = useMemo(() => {
    const rows = leaves.data ?? [];
    const term = staffSearch.trim().toLowerCase();
    if (!canReadOthers || !term || staffProfileId) return rows;
    return rows.filter((item) => `${item.staffName} ${item.employeeCode}`.toLowerCase().includes(term));
  }, [canReadOthers, leaves.data, staffProfileId, staffSearch]);

  const pending = items.filter((item) => item.status === "Pending");
  const dayItems = selectedDate ? items.filter((item) => coversDay(item, selectedDate)) : [];
  const denied = access.isSuccess && !canReadOthers && !canApply;
  const loadError = leaves.error instanceof ApiError ? leaves.error.message : null;

  const onRangeChange = useCallback((next: { from: string; to: string }) => {
    setRange((current) => (current.from === next.from && current.to === next.to ? current : next));
  }, []);

  return (
    <PageState
      error={denied ? "You do not have permission to view staff leave." : loadError}
      loading={(access.isLoading || leaves.isLoading) && !leaves.data}
      onRetry={() => void leaves.refetch()}
    >
      <div className="grid gap-5">
        <header className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="text-sm text-muted">Staff & Payroll</p>
            <h1 className="text-3xl font-semibold">Staff Leaves</h1>
            <p className="mt-1 text-sm text-muted">
              {canReadOthers
                ? "Review pending requests and approved leave on the same calendar."
                : "Apply for leave and track your own requests on the calendar."}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            {canApply ? (
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => { setNotice(null); setApplying(true); }} type="button">
                Apply for leave
              </button>
            ) : null}
            {canReadOthers ? (
              <>
                <input aria-label="Search staff" className="field min-w-[12rem]" onChange={(event) => setStaffSearch(event.target.value)} placeholder="Search staff…" value={staffSearch} />
                <select aria-label="Staff filter" className="field w-auto min-w-[10rem]" onChange={(event) => setStaffProfileId(event.target.value)} value={staffProfileId}>
                  <option value="">All staff</option>
                  {staffOptions.map((row) => <option key={row.id} value={row.id}>{row.firstName} {row.lastName} ({row.employeeCode})</option>)}
                </select>
              </>
            ) : null}
            <select aria-label="Status filter" className="field w-auto" onChange={(event) => setStatus(event.target.value)} value={status}>
              <option value="">All statuses</option>
              <option value="Pending">Pending</option>
              <option value="Approved">Approved</option>
              <option value="Rejected">Rejected</option>
              <option value="Cancelled">Cancelled</option>
            </select>
            <select aria-label="Type filter" className="field w-auto" onChange={(event) => setLeaveType(event.target.value)} value={leaveType}>
              <option value="">All types</option>
              <option value="Annual">Annual</option>
              <option value="Sick">Sick</option>
              <option value="Casual">Casual</option>
              <option value="Unpaid">Unpaid</option>
              <option value="Other">Other</option>
            </select>
          </div>
        </header>

        {notice ? <p role="status">{notice}</p> : null}

        <section className="grid gap-3 sm:grid-cols-3">
          <Summary label="In range" value={String(items.length)} />
          <Summary label="Pending" value={String(pending.length)} tone="warn" />
          <Summary label="Approved" value={String(items.filter((item) => item.status === "Approved").length)} />
        </section>

        <div className="grid gap-4 xl:grid-cols-[minmax(0,1.7fr)_minmax(18rem,1fr)]">
          <StaffLeaveCalendar
            items={items}
            loading={leaves.isFetching}
            onRangeChange={onRangeChange}
            onSelectDate={setSelectedDate}
            onSelectLeave={setSelectedId}
            onViewChange={setView}
            selectedDate={selectedDate}
            selectedId={selectedId}
            view={view}
          />
          <LeaveDetailsAside
            cancelError={cancel.error instanceof ApiError ? cancel.error.message : null}
            cancelling={cancel.isPending}
            dayItems={dayItems}
            detail={detail.data}
            detailError={detail.error instanceof ApiError ? detail.error.message : null}
            detailLoading={detail.isLoading}
            onCancelLeave={(id) => cancel.mutate(id)}
            onSelect={setSelectedId}
            onSelectDate={setSelectedDate}
            pending={pending}
            selectedDate={selectedDate}
            selectedId={selectedId}
          />
        </div>
      </div>
      {applying ? (
        <ApplyLeaveDialog
          onCancel={() => setApplying(false)}
          onSaved={async () => {
            setApplying(false);
            setNotice("Leave request submitted.");
            await client.invalidateQueries({ queryKey: ["staff-leaves", tenantId] });
            await client.invalidateQueries({ queryKey: ["staff-leave", tenantId] });
          }}
        />
      ) : null}
    </PageState>
  );
}

function Summary({ label, value, tone }: { label: string; value: string; tone?: "warn" }) {
  return (
    <article className={`rounded-2xl border px-4 py-4 shadow-sm ${tone === "warn" ? "border-amber-500/40 bg-amber-500/10" : "border-line bg-surface"}`}>
      <p className="text-sm text-muted">{label}</p>
      <p className="mt-2 text-xl font-semibold">{value}</p>
    </article>
  );
}
