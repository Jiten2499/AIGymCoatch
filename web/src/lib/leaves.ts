import { api } from "./api";

export type StaffLeaveListItem = {
  id: string;
  staffProfileId: string;
  userId: string;
  staffName: string;
  employeeCode: string;
  leaveType: string;
  startDate: string;
  endDate: string;
  durationDays: number;
  status: string;
  reason: string;
  approvalRequestId: string | null;
  submittedAt: string;
};

export type StaffLeaveDetails = StaffLeaveListItem & {
  attachmentFileId: string | null;
  decisionComment: string | null;
  decidedByUserId: string | null;
  decidedAt: string | null;
  canCancel: boolean;
};

export type LeaveView = "month" | "week" | "list";

export type LeaveCalendarEvent = {
  id: string;
  title: string;
  start: string;
  end: string;
  allDay: true;
  color: string;
  contrastColor: string;
  extendedProps: {
    leave: StaffLeaveListItem;
  };
};

export function listStaffLeaves(from: string, to: string, staffProfileId?: string, leaveType?: string, status?: string) {
  const query = new URLSearchParams({ from, to });
  if (staffProfileId) query.set("staffProfileId", staffProfileId);
  if (leaveType) query.set("leaveType", leaveType);
  if (status) query.set("status", status);
  return api<StaffLeaveListItem[]>(`/leaves?${query.toString()}`);
}

export function getStaffLeave(id: string) {
  return api<StaffLeaveDetails>(`/leaves/${id}`);
}

export function statusTone(status: string): string {
  if (status === "Approved") return "bg-accent/20 text-accent";
  if (status === "Pending" || status === "Planned") return "bg-amber-500/15 text-amber-200";
  if (status === "Rejected") return "bg-red-500/15 text-red-200";
  if (status === "Cancelled") return "bg-line/60 text-muted";
  return "bg-background text-muted";
}

export function leaveStatusColors(status: string): { backgroundColor: string; borderColor: string; textColor: string } {
  if (status === "Approved") return { backgroundColor: "#1f5c3a", borderColor: "#c8f54e", textColor: "#e8ffb0" };
  if (status === "Pending" || status === "Planned") return { backgroundColor: "#5c4518", borderColor: "#f5b942", textColor: "#ffe6a8" };
  if (status === "Rejected") return { backgroundColor: "#5c1f1f", borderColor: "#f07178", textColor: "#ffd0d3" };
  if (status === "Cancelled") return { backgroundColor: "#24332b", borderColor: "#5a7264", textColor: "#9aab9f" };
  return { backgroundColor: "#10211a", borderColor: "#1e3328", textColor: "#f3f7f2" };
}

/** FullCalendar all-day `end` is exclusive; domain leave endDate is inclusive. */
export function exclusiveEndDate(inclusiveEnd: string): string {
  const end = new Date(`${inclusiveEnd}T00:00:00Z`);
  end.setUTCDate(end.getUTCDate() + 1);
  return end.toISOString().slice(0, 10);
}

export function inclusiveEndFromExclusive(exclusiveEnd: string): string {
  const end = new Date(`${exclusiveEnd.slice(0, 10)}T00:00:00Z`);
  end.setUTCDate(end.getUTCDate() - 1);
  return end.toISOString().slice(0, 10);
}

export function leaveStatusClass(status: string): string {
  if (status === "Approved") return "lc-status-approved";
  if (status === "Pending" || status === "Planned") return "lc-status-pending";
  if (status === "Rejected") return "lc-status-rejected";
  if (status === "Cancelled") return "lc-status-cancelled";
  return "lc-status-other";
}

export function toLeaveCalendarEvents(items: readonly StaffLeaveListItem[]): LeaveCalendarEvent[] {
  return items.map((item) => {
    const colors = leaveStatusColors(item.status);
    return {
      id: item.id,
      title: `${item.staffName.split(" ")[0]} · ${item.leaveType}`,
      start: item.startDate,
      end: exclusiveEndDate(item.endDate),
      allDay: true,
      color: colors.backgroundColor,
      contrastColor: colors.textColor,
      extendedProps: { leave: item },
    };
  });
}

export function monthBounds(year: number, month: number): { from: string; to: string } {
  const from = new Date(Date.UTC(year, month - 1, 1));
  const to = new Date(Date.UTC(year, month, 0));
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

export function weekBounds(anchor: Date): { from: string; to: string } {
  const day = anchor.getUTCDay();
  const start = new Date(Date.UTC(anchor.getUTCFullYear(), anchor.getUTCMonth(), anchor.getUTCDate() - ((day + 6) % 7)));
  const end = new Date(start);
  end.setUTCDate(start.getUTCDate() + 6);
  return { from: start.toISOString().slice(0, 10), to: end.toISOString().slice(0, 10) };
}

export function daysInRange(from: string, to: string): string[] {
  const start = new Date(`${from}T00:00:00Z`);
  const end = new Date(`${to}T00:00:00Z`);
  const days: string[] = [];
  for (let cursor = new Date(start); cursor <= end; cursor.setUTCDate(cursor.getUTCDate() + 1)) {
    days.push(cursor.toISOString().slice(0, 10));
  }
  return days;
}

export function coversDay(item: StaffLeaveListItem, day: string): boolean {
  return item.startDate <= day && item.endDate >= day;
}
