export function freezeFormError(days: number, minDays: number, maxDays: number): string | null {
  if (!Number.isInteger(days) || days <= 0) {
    return "Enter the number of freeze days.";
  }
  const minimum = Math.max(minDays, 1);
  if (maxDays <= 0) {
    return "This gym does not allow a membership freeze.";
  }
  if (days < minimum || days > maxDays) {
    return `Freeze length must be between ${minimum} and ${maxDays} days.`;
  }
  return null;
}

export function addDays(isoDate: string, days: number): string {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}
