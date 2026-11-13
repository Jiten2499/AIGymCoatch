export const notificationTypes = ['WorkoutReminder', 'MembershipReminder', 'ChallengeReminder', 'AiRecommendation', 'WorkoutCompleted'] as const;

export type NotificationType = (typeof notificationTypes)[number];

export type AppNotification = {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
};

export type NotificationSummary = {
  unreadCount: number;
  latestActivityAt: string | null;
  serverTimeUtc: string;
  circularUnreadCount?: number;
};

export type FeedItem =
  | {
      kind: 'notification';
      id: string;
      title: string;
      message: string;
      isRead: boolean;
      occurredAt: string;
      notificationType: AppNotification['type'];
    }
  | {
      kind: 'circular';
      id: string;
      title: string;
      message: string;
      isRead: boolean;
      occurredAt: string;
      circularType: string;
      priority: string;
      createdBy: string | null;
      hasAttachment: boolean;
    };

export type NotificationSync = {
  items: AppNotification[];
  unreadCount: number;
  serverTimeUtc: string;
};
