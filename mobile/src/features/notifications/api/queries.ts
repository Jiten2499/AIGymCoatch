import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import type { PageResult } from '../../../types/api';
import { markQueryOnline, reportQueryConnectivity } from '../../../app/providers/queryLifecycle';
import { getCommunicationInboxSummary } from '../../communications/api/communications.api';
import { tenantKey } from '../../fitness/api/keys';
import { useTenantId } from '../../fitness/api/queries';
import type { AppNotification, NotificationSummary } from '../types';
import { getNotificationSummary, listNotifications, markNotificationRead, syncNotifications } from './notifications.api';
import { advanceCursor, mergeById, NOTIFICATION_LIST_POLL_MS, NOTIFICATION_SUMMARY_POLL_MS } from './polling';

export function notificationSummaryKey(tenantId: string) {
  return tenantKey(tenantId, 'notification-summary');
}

export function notificationsListKey(tenantId: string, page: number) {
  return tenantKey(tenantId, 'notifications', page);
}

export function notificationCursorKey(tenantId: string) {
  return tenantKey(tenantId, 'notification-sync-cursor');
}

async function withConnectivity<T>(run: () => Promise<T>): Promise<T> {
  try {
    const value = await run();
    markQueryOnline();
    return value;
  } catch (error) {
    reportQueryConnectivity(error);
    throw error;
  }
}

export function useNotificationSummary(enabled = true) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: notificationSummaryKey(tenantId),
    enabled: Boolean(tenantId) && enabled,
    refetchInterval: NOTIFICATION_SUMMARY_POLL_MS,
    refetchIntervalInBackground: false,
    refetchOnReconnect: true,
    refetchOnWindowFocus: true,
    queryFn: async ({ signal }) => {
      const summary = await withConnectivity(() => getNotificationSummary(signal));
      try {
        const circulars = await withConnectivity(() => getCommunicationInboxSummary(signal));
        return {
          ...summary,
          unreadCount: summary.unreadCount + circulars.unreadCount,
          circularUnreadCount: circulars.unreadCount,
        } satisfies NotificationSummary;
      } catch {
        // Circular inbox may be feature-gated; system notifications still poll.
        return { ...summary, circularUnreadCount: 0 } satisfies NotificationSummary;
      }
    },
  });
}

export function useNotifications(page: number, options?: { pollWhileActive?: boolean }) {
  const tenantId = useTenantId();
  const poll = options?.pollWhileActive === true;
  const client = useQueryClient();
  const cursorRef = useRef<string | null>(null);

  useEffect(() => {
    cursorRef.current = client.getQueryData<string | null>(notificationCursorKey(tenantId)) ?? null;
  }, [client, tenantId]);

  return useQuery({
    queryKey: notificationsListKey(tenantId, page),
    enabled: Boolean(tenantId),
    refetchInterval: poll && page === 1 ? NOTIFICATION_LIST_POLL_MS : false,
    refetchIntervalInBackground: false,
    refetchOnReconnect: true,
    refetchOnWindowFocus: true,
    queryFn: async ({ signal }) => {
      if (page !== 1 || !cursorRef.current) {
        const pageResult = await withConnectivity(() => listNotifications(page, signal));
        if (page === 1) {
          const summary = client.getQueryData<NotificationSummary>(notificationSummaryKey(tenantId));
          const nextCursor = advanceCursor(cursorRef.current, summary?.serverTimeUtc ?? new Date().toISOString());
          cursorRef.current = nextCursor;
          client.setQueryData(notificationCursorKey(tenantId), nextCursor);
        }
        return pageResult;
      }

      const sync = await withConnectivity(() => syncNotifications(cursorRef.current, 50, signal));
      cursorRef.current = advanceCursor(cursorRef.current, sync.serverTimeUtc);
      client.setQueryData(notificationCursorKey(tenantId), cursorRef.current);
      client.setQueryData(notificationSummaryKey(tenantId), {
        unreadCount: sync.unreadCount,
        latestActivityAt: sync.items[0]?.createdAt ?? null,
        serverTimeUtc: sync.serverTimeUtc,
      } satisfies NotificationSummary);

      const previous = client.getQueryData<PageResult<AppNotification>>(notificationsListKey(tenantId, 1));
      if (!previous) {
        return withConnectivity(() => listNotifications(1, signal));
      }

      if (sync.items.length === 0) {
        return previous;
      }

      const merged = mergeById(previous.items, sync.items).sort(
        (a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt),
      );
      const items = merged.slice(0, previous.pageSize);
      const added = sync.items.filter((item) => !previous.items.some((row) => row.id === item.id)).length;
      return {
        ...previous,
        items,
        totalCount: previous.totalCount + added,
      };
    },
  });
}

export function useMarkNotificationRead() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: async (notification) => {
      client.setQueriesData<PageResult<AppNotification>>(
        { queryKey: tenantKey(tenantId, 'notifications') },
        (current) => {
          if (!current) {
            return current;
          }
          return {
            ...current,
            items: current.items.map((item) => (item.id === notification.id ? notification : item)),
          };
        },
      );
      await client.invalidateQueries({ queryKey: notificationSummaryKey(tenantId) });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'notifications') });
    },
  });
}
