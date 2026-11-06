import { focusManager, onlineManager } from '@tanstack/react-query';
import { AppState, type AppStateStatus, type NativeEventSubscription } from 'react-native';
import { ApiError } from '../../services/api/problem';

let bound = false;
let appStateSub: NativeEventSubscription | null = null;

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

/** Keep onlineManager in sync with network / session failures for reconnect refetch. */
export function reportQueryConnectivity(error: unknown): void {
  if (error instanceof ApiError && error.offline) {
    onlineManager.setOnline(false);
    return;
  }

  onlineManager.setOnline(true);
}

export function markQueryOnline(): void {
  onlineManager.setOnline(true);
}
