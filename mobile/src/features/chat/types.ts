export type ChatRole = 'Member' | 'Trainer' | 'Staff' | 'Admin';

export type ChatPerson = {
  userId: string;
  displayName: string;
  role: ChatRole;
};

export type ChatInboxItem = {
  conversationId: string;
  otherUserId: string;
  displayName: string;
  role: ChatRole;
  lastPreview: string | null;
  lastMessageAt: string | null;
  unreadCount: number;
};

export type ChatMessageItem = {
  id: string;
  conversationId: string;
  senderUserId: string;
  messageType: 'Text' | 'Image' | 'Document';
  body: string;
  createdAt: string;
  isReadByOther: boolean;
  attachmentId: string | null;
  fileName: string | null;
  contentType: string | null;
  sizeBytes: number | null;
};

export type ChatSummary = {
  unreadConversations: number;
  unreadMessages: number;
};
