"use client";

import Link from "next/link";
import { statusTone, type StaffLeaveDetails, type StaffLeaveListItem } from "../../../lib/leaves";

export function LeaveDetailsAside({
  selectedDate,
  selectedId,
  dayItems,
  pending,
  detail,
  detailLoading,
  detailError,
  cancelling,
  cancelError,
  onSelect,
  onSelectDate,
  onCancelLeave,
}: {
  selectedDate: string | null;
  selectedId: string | null;
  dayItems: StaffLeaveListItem[];
  pending: StaffLeaveListItem[];
  detail: StaffLeaveDetails | undefined;
  detailLoading: boolean;
  detailError: string | null;
  cancelling: boolean;
  cancelError: string | null;
  onSelect: (id: string) => void;
  onSelectDate: (day: string) => void;
  onCancelLeave: (id: string) => void;
}) {
  return (
    <aside className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
      <h2 className="text-lg font-semibold">{selectedDate ? `Leaves on ${selectedDate}` : "Leave details"}</h2>
      {!selectedId && !selectedDate ? <p className="mt-2 text-sm text-muted">Select a date or leave entry to see details.</p> : null}
      {selectedDate && dayItems.length === 0 ? <p className="mt-3 rounded-xl border border-dashed border-line px-3 py-4 text-sm text-muted">No leave on this date.</p> : null}
      {selectedDate && dayItems.length > 0 ? (
        <ul className="mt-3 grid gap-2">
          {dayItems.map((item) => (
            <li key={item.id}>
              <DayLeaveButton item={item} onSelect={onSelect} selected={selectedId === item.id} />
            </li>
          ))}
        </ul>
      ) : null}
      {detailLoading ? <p className="mt-4" role="status">Loading leave details.</p> : null}
      {detailError ? <p className="mt-4" role="alert">{detailError}</p> : null}
      {detail ? (
        <div className="mt-4 grid gap-2 border-t border-line pt-4 text-sm">
          <h3 className="text-base font-semibold">{detail.staffName}</h3>
          <p className="text-muted">{detail.employeeCode} · {detail.leaveType}</p>
          <p><span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-medium ${statusTone(detail.status)}`}>{detail.status}</span></p>
          <p>{detail.startDate} → {detail.endDate} ({detail.durationDays} day{detail.durationDays === 1 ? "" : "s"})</p>
          <p>Reason: {detail.reason}</p>
          {detail.decisionComment ? <p>Decision: {detail.decisionComment}</p> : null}
          {detail.canCancel ? (
            <button className="mt-2 inline-flex min-h-11 items-center justify-center rounded-full border border-line px-4 disabled:opacity-60" disabled={cancelling} onClick={() => onCancelLeave(detail.id)} type="button">
              {cancelling ? "Cancelling..." : "Cancel request"}
            </button>
          ) : null}
          {cancelError ? <p role="alert">{cancelError}</p> : null}
          {detail.approvalRequestId ? (
            <Link className="mt-2 inline-flex min-h-11 items-center justify-center rounded-full bg-accent px-4 font-semibold text-background" href={`/business/tasks?requestId=${detail.approvalRequestId}`}>
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
                  onClick={() => { onSelect(item.id); onSelectDate(item.startDate); }}
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
  );
}

function DayLeaveButton({ item, selected, onSelect }: { item: StaffLeaveListItem; selected: boolean; onSelect: (id: string) => void }) {
  return (
    <button className={`w-full rounded-xl border px-3 py-2 text-left ${selected ? "border-accent bg-accent/10" : "border-line hover:border-accent"}`} onClick={() => onSelect(item.id)} type="button">
      <span className="block font-medium">{item.staffName}</span>
      <span className="text-xs text-muted">{item.leaveType} · {item.startDate} → {item.endDate} · {item.durationDays}d</span>
      <span className={`mt-1 inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${statusTone(item.status)}`}>{item.status}</span>
      <span className="mt-1 block text-xs text-muted line-clamp-2">{item.reason}</span>
    </button>
  );
}
