"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useCallback, useMemo, useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { api, ApiError } from "../../../lib/api";
import {
  coversDay,
  getStaffLeave,
  listStaffLeaves,
  monthBounds,
  statusTone,
  type LeaveView,
  type StaffLeaveListItem,
} from "../../../lib/leaves";
import { getSession, subscribeSession } from "../../../lib/session";
import type { Page } from "../../../lib/types";
import { StaffLeaveCalendar } from "./StaffLeaveCalendar";

type StaffOption = {
  id: string;
  employeeCode: string;
  firstName: string;
  lastName: string;
  status: string;
};

export default function StaffLeavesPage() {
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const now = new Date();
  const initial = monthBounds(now.getFullYear(), now.getMonth() + 1);
  const [range, setRange] = useState(initial);
  const [view, setView] = useState<LeaveView>("month");
  const [status, setStatus] = useState("");
  const [leaveType, setLeaveType] = useState("");
  const [staffProfileId, setStaffProfileId] = useState("");
  const [staffSearch, setStaffSearch] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);

  const staff = useQuery({
    queryKey: ["staff", tenantId, "leaves-filter"],
    enabled: tenantId !== null,
    queryFn: () => api<Page<StaffOption>>("/staff?page=1&pageSize=200"),
    retry: false,
  });

  const leaves = useQuery({
    queryKey: ["staff-leaves", tenantId, range.from, range.to, status, leaveType, staffProfileId],
    enabled: tenantId !== null,
    queryFn: () => listStaffLeaves(range.from, range.to, staffProfileId || undefined, leaveType || undefined, status || undefined),
  });

  const detail = useQuery({
    queryKey: ["staff-leave", tenantId, selectedId],
    enabled: tenantId !== null && selectedId !== null,
    queryFn: () => getStaffLeave(selectedId!),
  });

  const staffOptions = useMemo(() => {
    const rows = staff.data?.items ?? [];
    const term = staffSearch.trim().toLowerCase();
    if (!term) return rows;
    return rows.filter((row) => {
      const name = `${row.firstName} ${row.lastName} ${row.employeeCode}`.toLowerCase();
      return name.includes(term);
    });
  }, [staff.data?.items, staffSearch]);

  const items = useMemo(() => {
    const rows = leaves.data ?? [];
    const term = staffSearch.trim().toLowerCase();
    if (!term || staffProfileId) return rows;
    return rows.filter((item) => `${item.staffName} ${item.employeeCode}`.toLowerCase().includes(term));
  }, [leaves.data, staffProfileId, staffSearch]);

  const pending = items.filter((item) => item.status === "Pending");
  const dayItems = selectedDate ? items.filter((item) => coversDay(item, selectedDate)) : [];
  const denied = leaves.error instanceof ApiError && leaves.error.status === 403;

  const onRangeChange = useCallback((next: { from: string; to: string }) => {
    setRange((current) => (current.from === next.from && current.to === next.to ? current : next));
  }, []);

  return (
    <PageState
      error={denied ? "You do not have permission to view staff leave." : leaves.error instanceof ApiError ? leaves.error.message : null}
      loading={leaves.isLoading && !leaves.data}
      onRetry={() => void leaves.refetch()}
    >
      <div className="grid gap-5">
        <header className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="text-sm text-muted">Staff & Payroll</p>
            <h1 className="text-3xl font-semibold">Staff Leaves</h1>
            <p className="mt-1 text-sm text-muted">Review pending requests and approved leave on the same calendar.</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <input
              aria-label="Search staff"
              className="field min-w-[12rem]"
              onChange={(event) => setStaffSearch(event.target.value)}
              placeholder="Search staff…"
              value={staffSearch}
            />
            <select
              aria-label="Staff filter"
              className="field w-auto min-w-[10rem]"
              onChange={(event) => setStaffProfileId(event.target.value)}
              value={staffProfileId}
            >
              <option value="">All staff</option>
              {staffOptions.map((row) => (
                <option key={row.id} value={row.id}>
                  {row.firstName} {row.lastName} ({row.employeeCode})
                </option>
              ))}
            </select>
            <select aria-label="Status filter" className="field w-auto" onChange={(event) => setStatus(event.target.value)} value={status}>
              <option value="">All statuses</option>
              <option value="Pending">Pending</option>
              <option value="Approved">Approved</option>
              <option value="Rejected">Rejected</option>
              <option value="Cancelled">Cancelled</option>
              <option value="Planned">Planned</option>
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

          <aside className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
            <h2 className="text-lg font-semibold">{selectedDate ? `Leaves on ${selectedDate}` : "Leave details"}</h2>
            {!selectedId && !selectedDate ? <p className="mt-2 text-sm text-muted">Select a date or leave entry to see details.</p> : null}

            {selectedDate && dayItems.length === 0 ? (
              <p className="mt-3 rounded-xl border border-dashed border-line px-3 py-4 text-sm text-muted">No leave on this date.</p>
            ) : null}

            {selectedDate && dayItems.length > 0 ? (
              <ul className="mt-3 grid gap-2">
                {dayItems.map((item) => (
                  <li key={item.id}>
                    <DayLeaveButton item={item} onSelect={setSelectedId} selected={selectedId === item.id} />
                  </li>
                ))}
              </ul>
            ) : null}

            {detail.isLoading ? <p className="mt-4" role="status">Loading leave details.</p> : null}
            {detail.error instanceof ApiError ? <p className="mt-4" role="alert">{detail.error.message}</p> : null}
            {detail.data ? (
              <div className="mt-4 grid gap-2 border-t border-line pt-4 text-sm">
                <h3 className="text-base font-semibold">{detail.data.staffName}</h3>
                <p className="text-muted">{detail.data.employeeCode} · {detail.data.leaveType}</p>
                <p>
                  <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-medium ${statusTone(detail.data.status)}`}>{detail.data.status}</span>
                </p>
                <p>
                  {detail.data.startDate} → {detail.data.endDate} ({detail.data.durationDays} day{detail.data.durationDays === 1 ? "" : "s"})
                </p>
                <p>Reason: {detail.data.reason}</p>
                {detail.data.decisionComment ? <p>Decision: {detail.data.decisionComment}</p> : null}
                {detail.data.approvalRequestId ? (
                  <Link
                    className="mt-2 inline-flex min-h-11 items-center justify-center rounded-full bg-accent px-4 font-semibold text-background"
                    href={`/business/tasks?requestId=${detail.data.approvalRequestId}`}
                  >
                    Open in Tasks
                  </Link>
                ) : null}
              </div>
            ) : null}

            {pending.length > 0 ? (
              <div className="mt-6 border-t border-line pt-4">
                <h3 className="font-semibold">Pending requests</h3>
                <ul className="mt-2 grid gap-2 text-sm">
                  {pending.map((item) => (
                    <li key={item.id}>
                      <button
                        className="w-full rounded-xl border border-line px-3 py-2 text-left hover:border-accent"
                        onClick={() => {
                          setSelectedId(item.id);
                          setSelectedDate(item.startDate);
                        }}
                        type="button"
                      >
                        {item.staffName}: {item.leaveType} · {item.startDate}
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </aside>
        </div>
      </div>
    </PageState>
  );
}

function DayLeaveButton({ item, selected, onSelect }: { item: StaffLeaveListItem; selected: boolean; onSelect: (id: string) => void }) {
  return (
    <button
      className={`w-full rounded-xl border px-3 py-2 text-left ${selected ? "border-accent bg-accent/10" : "border-line hover:border-accent"}`}
      onClick={() => onSelect(item.id)}
      type="button"
    >
      <span className="block font-medium">{item.staffName}</span>
      <span className="text-xs text-muted">
        {item.leaveType} · {item.startDate} → {item.endDate} · {item.durationDays}d
      </span>
      <span className={`mt-1 inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${statusTone(item.status)}`}>{item.status}</span>
      <span className="mt-1 block text-xs text-muted line-clamp-2">{item.reason}</span>
    </button>
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
