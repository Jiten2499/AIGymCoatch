import { login as loginRequest, logout as logoutRequest, refreshSession, register as registerRequest, type LoginBody, type RegisterBody } from '../../features/auth/api/auth.api';
import { queryClient } from '../../app/providers/queryClient';
import { useSessionStore } from '../../store/sessionStore';
import { useWorkspaceStore } from '../../store/workspaceStore';
import type { AuthResponse } from '../../types/api';
import { setAuthBridge } from '../api/authBridge';
import { clearSession, readSession, saveSession } from './tokenStorage';

let refreshTask: Promise<string | null> | null = null;

export function registerSessionBridge(): void {
  setAuthBridge({
    getAccessToken: () => useSessionStore.getState().accessToken,
    refresh: refreshAccess,
    onSessionExpired: () => {
      void endSession();
    },
  });
}

export async function signIn(body: LoginBody, signal?: AbortSignal): Promise<void> {
  await applyAuth(await loginRequest(body, signal));
}

export async function signUp(body: RegisterBody, signal?: AbortSignal): Promise<void> {
  await applyAuth(await registerRequest(body, signal));
}

export async function restoreSession(): Promise<void> {
  const stored = await readSession();
  if (!stored) {
    useSessionStore.getState().markAnonymous();
    return;
  }
  useSessionStore.setState({
    accessToken: stored.accessToken,
    refreshToken: stored.refreshToken,
    expiresAt: stored.expiresAt,
  });
  const next = await refreshAccess();
  if (!next) {
    useSessionStore.getState().markAnonymous();
  }
}

export async function endSession(): Promise<void> {
  const refreshToken = useSessionStore.getState().refreshToken;
  try {
    if (refreshToken) {
      await logoutRequest(refreshToken);
    }
  } catch {
    // Local sign-out still proceeds when the network or token is already gone.
  }
  await clearSession();
  useSessionStore.getState().clearSession();
  useWorkspaceStore.getState().reset();
  queryClient.clear();
}

export function refreshAccess(): Promise<string | null> {
  refreshTask ??= refreshOnce().finally(() => {
    refreshTask = null;
  });
  return refreshTask;
}

async function refreshOnce(): Promise<string | null> {
  const refreshToken = useSessionStore.getState().refreshToken ?? (await readSession())?.refreshToken;
  if (!refreshToken) {
    return null;
  }
  try {
    const response = await refreshSession(refreshToken);
    await applyAuth(response);
    return response.accessToken;
  } catch {
    await clearSession();
    useSessionStore.getState().clearSession();
    useWorkspaceStore.getState().reset();
    return null;
  }
}

export async function applyAuth(response: AuthResponse): Promise<void> {
  const previousTenant = useSessionStore.getState().currentTenant?.id;
  await saveSession({
    accessToken: response.accessToken,
    refreshToken: response.refreshToken,
    expiresAt: response.expiresAt,
  });
  if (previousTenant && previousTenant !== response.currentTenant.id) {
    useWorkspaceStore.getState().reset();
    queryClient.removeQueries({ queryKey: ['tenant', previousTenant] });
  }
  useSessionStore.getState().setSession({
    accessToken: response.accessToken,
    refreshToken: response.refreshToken,
    expiresAt: response.expiresAt,
    user: response.user,
    currentTenant: response.currentTenant,
  });
}
