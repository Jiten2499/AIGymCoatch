import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tenantKey } from '../../fitness/api/keys';
import { useTenantId } from '../../fitness/api/queries';
import { listChatInbox, listChatMessages, markChatRead, openChat, searchChatRecipients, sendChatAttachment, sendChatMessage } from './chat.api';

export function chatInboxKey(tenantId: string, filter: string, term: string) {
  return tenantKey(tenantId, 'chat-inbox', filter, term);
}

export function chatMessagesKey(tenantId: string, conversationId: string) {
  return tenantKey(tenantId, 'chat-messages', conversationId);
}

export function useChatInbox(filter: string, term: string, enabled = true) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: chatInboxKey(tenantId, filter, term),
    enabled: Boolean(tenantId) && enabled,
    queryFn: ({ signal }) => listChatInbox(filter, term, signal),
  });
}

export function useChatRecipients(term: string, enabled: boolean) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'chat-recipients', term),
    enabled: Boolean(tenantId) && enabled,
    queryFn: ({ signal }) => searchChatRecipients(term, signal),
  });
}

export function useChatMessages(conversationId: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: chatMessagesKey(tenantId, conversationId),
    enabled: Boolean(tenantId && conversationId),
    queryFn: ({ signal }) => listChatMessages(conversationId, undefined, signal),
  });
}

export function useOpenChat() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (recipientUserId: string) => openChat(recipientUserId),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'chat-inbox') });
    },
  });
}

export function useSendChatMessage(conversationId: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: string) => sendChatMessage(conversationId, body, `${Date.now()}-${conversationId}`),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: chatMessagesKey(tenantId, conversationId) });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'chat-inbox') });
    },
  });
}

export function useSendChatAttachment(conversationId: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (file: { uri: string; fileName: string; contentType: string }) =>
      sendChatAttachment(conversationId, file.uri, file.fileName, file.contentType, `${Date.now()}-file-${conversationId}`),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: chatMessagesKey(tenantId, conversationId) });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'chat-inbox') });
    },
  });
}

export function useMarkChatRead(conversationId: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (messageId: string) => markChatRead(conversationId, messageId),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'chat-inbox') });
      await client.invalidateQueries({ queryKey: chatMessagesKey(tenantId, conversationId) });
    },
  });
}
