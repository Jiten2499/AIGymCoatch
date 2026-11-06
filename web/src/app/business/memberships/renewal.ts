export function addCalendarDays(iso: string, days: number): string {
  const [year, month, day] = iso.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

export function renewalStart(status: string, endDate: string, today: string): string | null {
  if (status === "Cancelled" || status === "Suspended" || status === "Pending") {
    return null;
  }

  const expired = status === "Expired" || endDate < today;
  return expired ? today : addCalendarDays(endDate, 1);
}
