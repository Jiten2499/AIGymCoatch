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
