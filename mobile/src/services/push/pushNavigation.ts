export type PushNotificationPayload = {
  notificationId?: string;
  communicationId?: string;
  tenantId?: string;
  type?: string;
  conversationId?: string;
  messageId?: string;
  referenceId?: string;
  referenceType?: string;
  senderId?: string;
};

export type PushDestination = {
  name: 'ChatThread' | 'WorkoutPlan' | 'WorkoutSummary' | 'Tabs' | 'Profile' | 'Notifications' | 'Challenges' | 'CommunicationDetail';
  params?: Record<string, unknown>;
};

export function destinationForPush(payload: PushNotificationPayload): PushDestination | null {
  const reference = payload.referenceId;
  switch (payload.type) {
    case 'chat.message':
      return payload.conversationId
        ? { name: 'ChatThread', params: { conversationId: payload.conversationId, title: 'Chat', role: 'Member' } }
        : { name: 'Notifications' };
    case 'workout.plan.updated':
      return reference ? { name: 'WorkoutPlan', params: { programId: reference } } : { name: 'Tabs', params: { screen: 'Workouts' } };
    case 'workout.reminder':
      return reference ? { name: 'WorkoutPlan', params: { programId: reference } } : { name: 'Tabs', params: { screen: 'Workouts' } };
    case 'workout.completed':
      return reference ? { name: 'WorkoutSummary', params: { sessionId: reference } } : { name: 'Tabs', params: { screen: 'Workouts' } };
    case 'trainer.assigned':
      return { name: 'Profile' };
    case 'progress.milestone':
      return { name: 'Challenges' };
    case 'circular.published':
      return payload.communicationId || reference
        ? { name: 'CommunicationDetail', params: { communicationId: payload.communicationId ?? reference } }
        : { name: 'Notifications' };
    case 'account.updated':
      return { name: 'Notifications' };
    default:
      return payload.notificationId ? { name: 'Notifications' } : null;
  }
}
