export type CommunicationType = 'Announcement' | 'Circular' | 'Offer' | 'Event' | 'Alert' | 'General';
export type CommunicationPriority = 'Low' | 'Normal' | 'High';

export type MemberCommunication = {
  id: string;
  type: CommunicationType;
  title: string;
  message: string;
  priority: CommunicationPriority;
  publishAt: string;
  expiresAt: string | null;
  isRead: boolean;
  imageStorageKey: string | null;
  createdBy?: string | null;
};

export type CommunicationInboxSummary = {
  unreadCount: number;
  latestActivityAt: string | null;
  serverTimeUtc: string;
};

export type CommunicationInboxSync = {
  items: MemberCommunication[];
  unreadCount: number;
  serverTimeUtc: string;
};
