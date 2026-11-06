export type ProgressRange = '3M' | '6M' | '1Y' | 'All';
export type LengthUnit = 'cm' | 'in';

const rangeMonths: Record<Exclude<ProgressRange, 'All'>, number> = { '3M': 3, '6M': 6, '1Y': 12 };

export function shiftMonths(isoDate: string, monthsDelta: number): string {
  const [year, month, day] = isoDate.slice(0, 10).split('-').map(Number);
  return new Date(Date.UTC(year, month - 1 + monthsDelta, day)).toISOString().slice(0, 10);
}

export function inProgressRange(date: string, range: ProgressRange, today: string): boolean {
  const day = date.slice(0, 10);
  if (day > today) {
    return false;
  }
  return range === 'All' || day >= shiftMonths(today, -rangeMonths[range]);
}

export function weightSeries(rows: { assessmentDate: string; weight: number | null }[], range: ProgressRange, today: string) {
  return rows
    .filter((row) => row.weight != null && inProgressRange(row.assessmentDate, range, today))
    .map((row) => ({ date: row.assessmentDate.slice(0, 10), weight: row.weight as number }))
    .sort((left, right) => left.date.localeCompare(right.date));
}

export function formatProgressDate(iso: string): string {
  const [year, month, day] = iso.slice(0, 10).split('-').map(Number);
  return new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' }).format(new Date(Date.UTC(year, month - 1, day)));
}

export function formatMonth(iso: string): string {
  const [year, month, day] = iso.slice(0, 10).split('-').map(Number);
  return new Intl.DateTimeFormat('en', { month: 'short', timeZone: 'UTC' }).format(new Date(Date.UTC(year, month - 1, day)));
}

export function trimNumber(value: number): string {
  const rounded = Math.round(value * 10) / 10;
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1);
}

export function convertLength(value: string, from: LengthUnit, to: LengthUnit): string {
  if (from === to || value.trim().length === 0) {
    return value;
  }
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    return value;
  }
  const centimetres = from === 'in' ? parsed * 2.54 : parsed;
  return trimNumber(to === 'in' ? centimetres / 2.54 : centimetres);
}

export function lengthToCentimetres(value: number | null | 'invalid', unit: LengthUnit): number | null | 'invalid' {
  if (value == null || value === 'invalid') {
    return value;
  }
  const centimetres = unit === 'in' ? Math.round(value * 254) / 100 : value;
  return centimetres > 0 && centimetres <= 500 ? centimetres : 'invalid';
}

export function progressPhotoIssue(mimeType: string, fileSize?: number | null): 'type' | 'size' | null {
  if (mimeType !== 'image/jpeg' && mimeType !== 'image/png' && mimeType !== 'image/webp') {
    return 'type';
  }
  return fileSize != null && fileSize > 2_097_152 ? 'size' : null;
}
