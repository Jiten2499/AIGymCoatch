import { focusManager, onlineManager } from '@tanstack/react-query';
import { AppState, type AppStateStatus, type NativeEventSubscription } from 'react-native';
import { ApiError } from '../../services/api/problem';

let bound = false;
let appStateSub: NativeEventSubscription | null = null;
let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
let reconnectAttempts = 0;
const reconnectDelayMs = 700;
const maxAutoReconnects = 3;

function clearReconnectTimer(): void {
  if (reconnectTimer) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }
}

function isActive(status: AppStateStatus | string | null | undefined): boolean {
  return status === 'active';
}

/** Pause TanStack Query observers in background; resume and refetch on foreground. */
export function bindQueryLifecycle(): () => void {
  if (bound) {
    return () => undefined;
  }

  bound = true;
  focusManager.setFocused(isActive(AppState.currentState as AppStateStatus));
  onlineManager.setOnline(true);

  focusManager.setEventListener((handleFocus) => {
    appStateSub?.remove();
    appStateSub = AppState.addEventListener('change', (status) => {
      const active = isActive(status);
      handleFocus(active);
      if (active) {
        reconnectAttempts = 0;
        clearReconnectTimer();
        onlineManager.setOnline(true);
      }
    });
    return () => {
      appStateSub?.remove();
      appStateSub = null;
    };
  });

  return () => {
    appStateSub?.remove();
    appStateSub = null;
    bound = false;
  };
}

/** Retry a cold production connection. Auth and missing records are final. */
export function shouldRetryQuery(failureCount: number, error: unknown): boolean {
  const apiError = error instanceof ApiError ? error : null;
  const status = apiError?.status ?? 0;
  if (status === 401 || status === 403 || status === 404) {
    return false;
  }

  return failureCount < (apiError?.offline ? 3 : 1);
}

/** Keep onlineManager in sync with network / session failures for reconnect refetch. */
export function reportQueryConnectivity(error: unknown): void {
  if (error instanceof ApiError && error.offline) {
    onlineManager.setOnline(false);
    if (!reconnectTimer && reconnectAttempts < maxAutoReconnects) {
      reconnectAttempts += 1;
      reconnectTimer = setTimeout(() => {
        reconnectTimer = null;
        onlineManager.setOnline(true);
      }, reconnectDelayMs);
    }
    return;
  }

  markQueryOnline();
}

export function markQueryOnline(): void {
  clearReconnectTimer();
  reconnectAttempts = 0;
  if (!onlineManager.isOnline()) {
    onlineManager.setOnline(true);
  }
}
