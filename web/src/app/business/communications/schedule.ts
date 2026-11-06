export type ScheduleMode = "draft" | "now" | "later";

export function localDateTimeValue(value: string): string | null {
  const trimmed = value.trim();
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(trimmed)) {
    return null;
  }
  return `${trimmed}:00`;
}

export function formatWallClock(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(value);
  if (!match) {
    return value;
  }
  const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]), Number(match[4]), Number(match[5])));
  return new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "numeric", minute: "2-digit", hour12: true, timeZone: "UTC" }).format(date);
}

export function formatInZone(iso: string | null, timeZone: string): string {
  if (!iso) {
    return "—";
  }
  return new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "numeric", minute: "2-digit", hour12: true, timeZone }).format(new Date(iso));
}

export function toDateTimeLocal(iso: string, timeZone: string): string {
  const parts = new Intl.DateTimeFormat("en-CA", { timeZone, year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hourCycle: "h23" }).formatToParts(new Date(iso));
  const read = (type: string) => parts.find((part) => part.type === type)?.value ?? "";
  return `${read("year")}-${read("month")}-${read("day")}T${read("hour")}:${read("minute")}`;
}

export function canPublishNow(status: string): boolean {
  return status === "Draft" || status === "Scheduled";
}

export function canEditCommunication(status: string): boolean {
  return status === "Draft" || status === "Scheduled";
}

export function canCancelCommunication(status: string): boolean {
  return status === "Scheduled" || status === "Published";
}
