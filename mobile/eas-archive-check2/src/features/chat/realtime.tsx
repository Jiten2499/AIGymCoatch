import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { AppState } from 'react-native';
import { acceptCircularEvent, refreshCircularQueries, type CircularPublishedEvent } from '../communications/realtime';
import { tenantKey } from '../fitness/api/keys';
import { useSessionStore } from '../../store/sessionStore';
import { chatHubUrl } from './api/chat.api';

const RealtimeContext = createContext<HubConnection | null>(null);

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const client = useQueryClient();
  const token = useSessionStore((state) => state.accessToken);
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  const [connection, setConnection] = useState<HubConnection | null>(null);

  useEffect(() => {
    if (!token || !tenantId) {
      setConnection(null);
      return undefined;
    }

    const hub = new HubConnectionBuilder()
      .withUrl(chatHubUrl(), { accessTokenFactory: () => useSessionStore.getState().accessToken ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Error)
      .build();
    const refresh = () => {
      const current = useSessionStore.getState().currentTenant?.id ?? '';
      if (current) {
        refreshCircularQueries(client, current);
      }
    };
    const onPublished = (event: CircularPublishedEvent) => {
      const current = useSessionStore.getState().currentTenant?.id ?? '';
      if (acceptCircularEvent(event, current)) {
        refreshCircularQueries(client, current);
      }
    };
    hub.on('CircularPublished', onPublished);
    hub.onreconnected(refresh);
    const appState = AppState.addEventListener('change', (status) => {
      if (status === 'active') {
        refresh();
      }
    });
    let stopped = false;
    void hub.start().then(() => {
      if (!stopped) {
        setConnection(hub);
      }
    }).catch(() => undefined);

    return () => {
      stopped = true;
      appState.remove();
      hub.off('CircularPublished', onPublished);
      void hub.stop();
      setConnection(null);
    };
  }, [client, tenantId, token]);

  return <RealtimeContext.Provider value={connection}>{children}</RealtimeContext.Provider>;
}

export function useChatRealtime(conversationId?: string, onTyping?: (name: string | null) => void) {
  const connection = useContext(RealtimeContext);
  const client = useQueryClient();
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  const typingRef = useRef(onTyping);
  typingRef.current = onTyping;

  useEffect(() => {
    if (!connection || !tenantId) {
      return undefined;
    }

    const refresh = () => {
      void client.invalidateQueries({ queryKey: tenantKey(tenantId, 'chat-inbox') });
      if (conversationId) {
        void client.invalidateQueries({ queryKey: tenantKey(tenantId, 'chat-messages', conversationId) });
      }
    };
    const onStarted = () => typingRef.current?.('typing');
    const onStopped = () => typingRef.current?.(null);
    connection.on('MessageReceived', refresh);
    connection.on('MessageRead', refresh);
    connection.on('ConversationUpdated', refresh);
    connection.on('TypingStarted', onStarted);
    connection.on('TypingStopped', onStopped);
    connection.onreconnected(() => {
      refresh();
      if (conversationId) {
        void connection.invoke('JoinConversation', conversationId);
      }
    });
    if (conversationId) {
      void connection.invoke('JoinConversation', conversationId).catch(() => undefined);
    }

    return () => {
      connection.off('MessageReceived', refresh);
      connection.off('MessageRead', refresh);
      connection.off('ConversationUpdated', refresh);
      connection.off('TypingStarted', onStarted);
      connection.off('TypingStopped', onStopped);
    };
  }, [client, connection, conversationId, tenantId]);
}
