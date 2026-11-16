import { CommonActions } from '@react-navigation/native';
import { isRunningInExpoGo } from 'expo';
import { AppState, Platform } from 'react-native';
import { navigationRef } from '../../app/navigation/navigationRef';
import { queryClient } from '../../app/providers/queryClient';
import { invalidateChatLists } from '../../features/chat/api/queries';
import { markNotificationRead } from '../../features/notifications/api/notifications.api';
import { notificationSummaryKey } from '../../features/notifications/api/queries';
import { apiClient } from '../api/client';
import { getSecretDriver } from '../storage/secretDriver';
import { useSessionStore } from '../../store/sessionStore';
import { destinationForPush, type PushNotificationPayload } from './pushNavigation';

export type { PushNotificationPayload } from './pushNavigation';

export type PushRegistration = {
  platform: 'expo' | 'fcm' | 'apns';
  token: string;
};

const installationKey = 'growfit.push.installation';
const listeners = new Set<(payload: PushNotificationPayload) => void>();
let pending: PushNotificationPayload | null = null;
let listening = false;
let lastToken: string | null = null;
let channelsReady = false;
let expoGoWarned = false;

function androidPushInExpoGo(): boolean {
  return Platform.OS === 'android' && isRunningInExpoGo();
}

function warnExpoGoPush(): void {
  if (!androidPushInExpoGo() || expoGoWarned || typeof __DEV__ === 'undefined' || !__DEV__) {
    return;
  }
  expoGoWarned = true;
  console.warn('Android push notifications need a Growfit development build. Expo Go cannot register a device token.');
}

export function subscribePushNotifications(listener: (payload: PushNotificationPayload) => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function notifyPushReceived(payload: PushNotificationPayload): void {
  for (const listener of listeners) {
    listener(payload);
  }
}

export async function registerPushToken(registration: PushRegistration): Promise<void> {
  if (process.env.NODE_ENV === 'test' || !registration.token) {
    return;
  }
  await syncPushRegistration();
}

export async function syncPushRegistration(): Promise<void> {
  if (process.env.NODE_ENV === 'test' || (Platform.OS !== 'android' && Platform.OS !== 'ios')) {
    return;
  }
  if (useSessionStore.getState().status !== 'authenticated') {
    return;
  }
  if (androidPushInExpoGo()) {
    warnExpoGoPush();
    return;
  }

  try {
    const Notifications = await import('expo-notifications');
    const Constants = await import('expo-constants');
    await ensureChannels(true, true, false);
    const current = await Notifications.getPermissionsAsync();
    const permission = current.granted || current.canAskAgain === false ? current : await Notifications.requestPermissionsAsync();
    if (!permission.granted) {
      return;
    }

    const projectId = Constants.default.easConfig?.projectId ?? Constants.default.expoConfig?.extra?.eas?.projectId;
    if (!projectId) {
      return;
    }

    const token = (await Notifications.getExpoPushTokenAsync({ projectId })).data;
    if (!token || token === lastToken) {
      return;
    }

    await apiClient.post('/notifications/devices', {
      installationId: await installationId(),
      expoPushToken: token,
      platform: Platform.OS,
      appVersion: Constants.default.nativeApplicationVersion ?? Constants.default.expoConfig?.version ?? null,
    });
    lastToken = token;
  } catch (error) {
    if (typeof __DEV__ !== 'undefined' && __DEV__) {
      console.error('Push registration failed', error instanceof Error ? error.message : 'Request failed');
    }
  }
}

export async function deactivatePushRegistration(): Promise<void> {
  lastToken = null;
  if (process.env.NODE_ENV === 'test') {
    return;
  }

  try {
    const installation = await getSecretDriver().get(installationKey);
    if (installation) {
      await apiClient.delete(`/notifications/devices/${encodeURIComponent(installation)}`);
    }
  } catch {
    // Sign-out still completes when the device record is already gone.
  }
}

export async function configurePushChannels(sound: boolean, vibration: boolean): Promise<void> {
  if (process.env.NODE_ENV === 'test' || Platform.OS !== 'android') {
    return;
  }
  await ensureChannels(sound, vibration, true);
}

export async function readPushPermission(): Promise<'granted' | 'denied' | 'unknown'> {
  if (process.env.NODE_ENV === 'test' || (Platform.OS !== 'android' && Platform.OS !== 'ios')) {
    return 'unknown';
  }
  try {
    const Notifications = await import('expo-notifications');
    const permission = await Notifications.getPermissionsAsync();
    return permission.granted ? 'granted' : 'denied';
  } catch {
    return 'unknown';
  }
}

export function rememberPush(payload: PushNotificationPayload): void {
  pending = payload;
}

export function consumePendingPush(): void {
  if (pending) {
    const payload = pending;
    pending = null;
    routePush(payload);
  }
}

export function routePush(payload: PushNotificationPayload): void {
  if (useSessionStore.getState().status !== 'authenticated') {
    pending = payload;
    return;
  }

  const tenantId = useSessionStore.getState().currentTenant?.id ?? '';
  if (payload.tenantId && tenantId && payload.tenantId !== tenantId) {
    return;
  }

  const destination = destinationForPush(payload);
  if (!destination || !navigationRef.isReady()) {
    pending = payload;
    return;
  }

  navigationRef.dispatch(CommonActions.navigate({ name: destination.name, params: destination.params }));
  if (tenantId && payload.type === 'chat.message' && payload.conversationId) {
    invalidateChatLists(queryClient, tenantId, payload.conversationId);
  }
  if (tenantId) {
    void queryClient.invalidateQueries({ queryKey: notificationSummaryKey(tenantId) });
  }
  if (payload.notificationId) {
    void markNotificationRead(payload.notificationId).catch(() => undefined);
  }
}

export async function startPushListeners(): Promise<void> {
  if (listening || process.env.NODE_ENV === 'test') {
    return;
  }
  listening = true;
  const Notifications = await import('expo-notifications');
  Notifications.setNotificationHandler({
    handleNotification: async () => ({
      shouldShowBanner: true,
      shouldShowList: true,
      shouldPlaySound: true,
      shouldSetBadge: false,
    }),
  });
  Notifications.addNotificationReceivedListener((event) => {
    notifyPushReceived(readPayload(event.request.content.data));
  });
  Notifications.addNotificationResponseReceivedListener((event) => {
    routePush(readPayload(event.notification.request.content.data));
  });
  const last = await Notifications.getLastNotificationResponseAsync();
  if (last) {
    routePush(readPayload(last.notification.request.content.data));
  }
  AppState.addEventListener('change', (state) => {
    if (state === 'active') {
      void syncPushRegistration();
    }
  });
}

async function ensureChannels(sound: boolean, vibration: boolean, force: boolean): Promise<void> {
  if (Platform.OS !== 'android' || androidPushInExpoGo() || (channelsReady && !force)) {
    return;
  }
  const Notifications = await import('expo-notifications');
  const channels = [
    { id: 'chat_messages', name: 'Chat messages', importance: Notifications.AndroidImportance.HIGH },
    { id: 'workout_reminders', name: 'Workout reminders', importance: Notifications.AndroidImportance.DEFAULT },
    { id: 'workout_updates', name: 'Workout updates', importance: Notifications.AndroidImportance.DEFAULT },
    { id: 'account_notifications', name: 'Account notifications', importance: Notifications.AndroidImportance.DEFAULT },
  ];
  for (const channel of channels) {
    try {
      await Notifications.setNotificationChannelAsync(channel.id, {
        name: channel.name,
        importance: channel.importance,
        lightColor: '#06130D',
        showBadge: true,
        enableVibrate: vibration,
        ...(sound ? {} : { sound: null }),
      });
    } catch (error) {
      // The native module can reject while converting a null channel field back to JavaScript
      // after the channel has already been saved. Registration must continue.
      if (typeof __DEV__ !== 'undefined' && __DEV__) {
        console.warn('Notification channel setup skipped', channel.id, error instanceof Error ? error.message : 'failed');
      }
    }
  }
  channelsReady = true;
}

async function installationId(): Promise<string> {
  const existing = await getSecretDriver().get(installationKey);
  if (existing) {
    return existing;
  }
  const created = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (char) => {
    const value = Math.floor(Math.random() * 16);
    return (char === 'x' ? value : (value & 0x3) | 0x8).toString(16);
  });
  await getSecretDriver().set(installationKey, created);
  return created;
}

function readPayload(data: Record<string, unknown> | undefined): PushNotificationPayload {
  return {
    notificationId: text(data?.notificationId),
    communicationId: text(data?.communicationId),
    tenantId: text(data?.tenantId),
    type: text(data?.type),
    conversationId: text(data?.conversationId),
    messageId: text(data?.messageId),
    referenceId: text(data?.referenceId),
    referenceType: text(data?.referenceType),
    senderId: text(data?.senderId),
  };
}

function text(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}
