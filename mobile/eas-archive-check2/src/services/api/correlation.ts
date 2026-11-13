export function createCorrelationId(): string {
  const time = Date.now().toString(36);
  const random = Math.random().toString(36).slice(2, 12);
  return `lf-${time}-${random}`.slice(0, 64);
}
