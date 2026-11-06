export type OptionalNumber = number | null | 'invalid';

export function parseOptionalMeasure(value: string): OptionalNumber {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return null;
  }
  const parsed = Number(trimmed);
  if (!Number.isFinite(parsed) || parsed <= 0 || parsed > 500) {
    return 'invalid';
  }
  return parsed;
}

export function parseOptionalPercent(value: string): OptionalNumber {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return null;
  }
  const parsed = Number(trimmed);
  if (!Number.isFinite(parsed) || parsed < 0 || parsed > 100) {
    return 'invalid';
  }
  return parsed;
}

export function lengthUnit(unitSystem: string): string {
  return unitSystem === 'Imperial' ? 'in' : 'cm';
}

export function weightUnit(unitSystem: string): string {
  return unitSystem === 'Imperial' ? 'lb' : 'kg';
}

export function formatMeasure(value: number | null, unit: string): string {
  return value == null ? '—' : `${value} ${unit}`;
}

export function formatChange(change: number | null, unit: string): string | null {
  if (change == null) {
    return null;
  }
  return `${change > 0 ? '+' : ''}${change} ${unit}`;
}
