/**
 * Push delivery stays separate from TanStack Query polling.
 * Register Expo Notifications / FCM / APNs handlers here later; on receipt,
 * invalidate notification summary + list keys instead of starting another poll loop.
 */
export type PushNotificationPayload = {
  notificationId?: string;
  communicationId?: string;
  tenantId?: string;
  type?: string;
};

export type PushRegistration = {
  platform: 'expo' | 'fcm' | 'apns';
  token: string;
};

type PushListener = (payload: PushNotificationPayload) => void;

const listeners = new Set<PushListener>();

export function subscribePushNotifications(listener: PushListener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/** Invoked by a future Expo Notifications / FCM / APNs adapter when a push arrives in-foreground. */
export function notifyPushReceived(payload: PushNotificationPayload): void {
  for (const listener of listeners) {
    listener(payload);
  }
}

/** Placeholder until device registration endpoints exist. */
export async function registerPushToken(_registration: PushRegistration): Promise<void> {
  return undefined;
}
