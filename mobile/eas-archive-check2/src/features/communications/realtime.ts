import type { QueryClient } from '@tanstack/react-query';
import { tenantKey } from '../fitness/api/keys';
import { notificationSummaryKey } from '../notifications/api/queries';
import { communicationSummaryKey, communicationUnreadKey } from './api/queries';

export type CircularPublishedEvent = {
  communicationId?: string;
  tenantId?: string;
  publishedAtUtc?: string;
  audience?: string;
};

export function acceptCircularEvent(event: CircularPublishedEvent | null | undefined, tenantId: string): boolean {
  return Boolean(tenantId && event?.communicationId && event.tenantId === tenantId);
}

export function refreshCircularQueries(client: QueryClient, tenantId: string): void {
  void client.invalidateQueries({ queryKey: tenantKey(tenantId, 'member-communications') });
  void client.invalidateQueries({ queryKey: tenantKey(tenantId, 'member-communication') });
  void client.invalidateQueries({ queryKey: communicationSummaryKey(tenantId) });
  void client.invalidateQueries({ queryKey: communicationUnreadKey(tenantId) });
  void client.invalidateQueries({ queryKey: notificationSummaryKey(tenantId) });
}
