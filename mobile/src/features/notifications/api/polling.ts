/** Shared polling intervals for in-app notification/communication sync (not push). */
export const NOTIFICATION_SUMMARY_POLL_MS = 30_000;
export const NOTIFICATION_LIST_POLL_MS = 60_000;

export type SyncCursor = string | null;

export function mergeById<T extends { id: string }>(existing: readonly T[], incoming: readonly T[]): T[] {
  if (incoming.length === 0) {
    return [...existing];
  }

  const map = new Map<string, T>();
  for (const item of existing) {
    map.set(item.id, item);
  }

  for (const item of incoming) {
    map.set(item.id, item);
  }

  return [...map.values()];
}

export function advanceCursor(current: SyncCursor, serverTimeUtc: string): string {
  if (!current) {
    return serverTimeUtc;
  }

  return Date.parse(serverTimeUtc) >= Date.parse(current) ? serverTimeUtc : current;
}
