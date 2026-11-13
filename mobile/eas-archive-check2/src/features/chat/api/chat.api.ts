import { apiClient } from '../../../services/api/client';
import { apiBaseUrl } from '../../../services/api/config';
import type { ChatInboxItem, ChatMessageItem, ChatPerson, ChatSummary } from '../types';

export function chatHubUrl(): string {
  return `${apiBaseUrl}/hubs/chat`;
}

export async function listChatInbox(filter: string, term: string, signal?: AbortSignal): Promise<ChatInboxItem[]> {
  const response = await apiClient.get<ChatInboxItem[]>('/chat/conversations', { params: { filter, term: term || undefined, take: 30 }, signal });
  return response.data;
}

export async function searchChatRecipients(term: string, signal?: AbortSignal): Promise<ChatPerson[]> {
  const response = await apiClient.get<ChatPerson[]>('/chat/recipients', { params: { term: term || undefined }, signal });
  return response.data;
}

export async function openChat(recipientUserId: string): Promise<ChatInboxItem> {
  const response = await apiClient.post<ChatInboxItem>('/chat/conversations', { recipientUserId });
  return response.data;
}

export async function listChatMessages(conversationId: string, before?: string, signal?: AbortSignal): Promise<ChatMessageItem[]> {
  const response = await apiClient.get<ChatMessageItem[]>(`/chat/conversations/${conversationId}/messages`, { params: { before, take: 30 }, signal });
  return response.data;
}

export async function sendChatMessage(conversationId: string, body: string, clientMessageId: string): Promise<ChatMessageItem> {
  const response = await apiClient.post<ChatMessageItem>(`/chat/conversations/${conversationId}/messages`, { body, clientMessageId });
  return response.data;
}

export async function sendChatAttachment(conversationId: string, uri: string, fileName: string, contentType: string, clientMessageId: string): Promise<ChatMessageItem> {
  const body = new FormData();
  body.append('file', { uri, name: fileName, type: contentType } as unknown as Blob);
  body.append('clientMessageId', clientMessageId);
  const response = await apiClient.post<ChatMessageItem>(`/chat/conversations/${conversationId}/attachments`, body, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return response.data;
}

export async function markChatRead(conversationId: string, messageId: string): Promise<void> {
  await apiClient.post(`/chat/conversations/${conversationId}/read`, { messageId });
}

export async function getChatSummary(signal?: AbortSignal): Promise<ChatSummary> {
  const response = await apiClient.get<ChatSummary>('/chat/summary', { signal });
  return response.data;
}
