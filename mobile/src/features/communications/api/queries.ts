import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { markQueryOnline, reportQueryConnectivity } from '../../../app/providers/queryLifecycle';
import type { PageResult } from '../../../types/api';
import { tenantKey } from '../../fitness/api/keys';
import { useTenantId } from '../../fitness/api/queries';
import { notificationSummaryKey } from '../../notifications/api/queries';
import { advanceCursor, mergeById } from '../../notifications/api/polling';
import type { CommunicationInboxSummary, CommunicationType, MemberCommunication } from '../types';
import {
  getCommunicationInboxSummary,
  getMemberCommunication,
  listMemberCommunications,
  markCommunicationRead,
  syncInboxCommunications,
} from './communications.api';

export function communicationUnreadKey(tenantId: string) {
  return tenantKey(tenantId, 'communication-unread');
}

export function communicationSummaryKey(tenantId: string) {
  return tenantKey(tenantId, 'communication-summary');
}

export function communicationListKey(tenantId: string, page: number, type: string, unread: string) {
  return tenantKey(tenantId, 'member-communications', page, type, unread);
}

export function communicationCursorKey(tenantId: string) {
  return tenantKey(tenantId, 'communication-sync-cursor');
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

export function useCommunicationInboxSummary(enabled = true) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: communicationSummaryKey(tenantId),
    enabled: Boolean(tenantId) && enabled,
    refetchInterval: false,
    refetchIntervalInBackground: false,
    refetchOnReconnect: true,
    refetchOnWindowFocus: true,
    queryFn: ({ signal }) => withConnectivity(() => getCommunicationInboxSummary(signal)),
  });
}

export function useUnreadCommunicationCount(enabled = true) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: communicationUnreadKey(tenantId),
    enabled: Boolean(tenantId) && enabled,
    refetchInterval: false,
    refetchOnReconnect: true,
    refetchOnWindowFocus: true,
    queryFn: async ({ signal }) => {
      const summary = await withConnectivity(() => getCommunicationInboxSummary(signal));
      return summary.unreadCount;
    },
  });
}

export function useMemberCommunications(
  page: number,
  type?: CommunicationType | null,
  enabled = true,
  unreadOnly = false,
) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  const typeKey = type ?? 'all';
  const unreadKey = unreadOnly ? 'unread' : 'any';
  const cursorRef = useRef<string | null>(null);

  useEffect(() => {
    cursorRef.current = client.getQueryData<string | null>(communicationCursorKey(tenantId)) ?? null;
  }, [client, tenantId]);

  return useQuery({
    queryKey: communicationListKey(tenantId, page, typeKey, unreadKey),
    enabled: Boolean(tenantId) && enabled,
    refetchInterval: false,
    refetchIntervalInBackground: false,
    refetchOnReconnect: true,
    refetchOnWindowFocus: true,
    queryFn: async ({ signal }) => {
      if (page !== 1 || unreadOnly || typeKey !== 'all' || !cursorRef.current) {
        const pageResult = await withConnectivity(() => listMemberCommunications(page, type, unreadOnly, signal));
        if (page === 1 && !unreadOnly && typeKey === 'all') {
          const summary = client.getQueryData<CommunicationInboxSummary>(communicationSummaryKey(tenantId));
          const next = advanceCursor(cursorRef.current, summary?.serverTimeUtc ?? new Date().toISOString());
          cursorRef.current = next;
          client.setQueryData(communicationCursorKey(tenantId), next);
        }
        return pageResult;
      }

      const sync = await withConnectivity(() => syncInboxCommunications(cursorRef.current, 30, signal));
      cursorRef.current = advanceCursor(cursorRef.current, sync.serverTimeUtc);
      client.setQueryData(communicationCursorKey(tenantId), cursorRef.current);
      client.setQueryData(communicationSummaryKey(tenantId), {
        unreadCount: sync.unreadCount,
        latestActivityAt: sync.items[0]?.publishAt ?? null,
        serverTimeUtc: sync.serverTimeUtc,
      } satisfies CommunicationInboxSummary);
      client.setQueryData(communicationUnreadKey(tenantId), sync.unreadCount);

      const previous = client.getQueryData<PageResult<MemberCommunication>>(communicationListKey(tenantId, 1, 'all', 'any'));
      if (!previous) {
        return withConnectivity(() => listMemberCommunications(1, null, false, signal));
      }

      if (sync.items.length === 0) {
        return previous;
      }

      const merged = mergeById(previous.items, sync.items).sort(
        (a, b) => Date.parse(b.publishAt) - Date.parse(a.publishAt),
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

export function useMemberCommunication(id: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'member-communication', id),
    enabled: Boolean(tenantId && id),
    queryFn: ({ signal }) => withConnectivity(() => getMemberCommunication(id, signal)),
  });
}

export function useMarkCommunicationRead() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => markCommunicationRead(id),
    onSuccess: async (_data, id) => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'member-communications') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'member-communication', id) });
      await client.invalidateQueries({ queryKey: communicationUnreadKey(tenantId) });
      await client.invalidateQueries({ queryKey: communicationSummaryKey(tenantId) });
      await client.invalidateQueries({ queryKey: notificationSummaryKey(tenantId) });
    },
  });
}
