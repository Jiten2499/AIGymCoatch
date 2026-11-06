export type AdminUser = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  hasPhoto?: boolean;
};

export type TenantSummary = {
  id: string;
  name: string;
  type: string;
  status: string;
};

export type WorkspaceSummary = TenantSummary & {
  roles: string[];
};

export type AdminSession = {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AdminUser;
  currentTenant?: TenantSummary;
};

const storageKey = "lifefit.session";
const activityKey = "lifefit.lastActivity";

export const idleLimitMs = 8 * 60 * 60 * 1000;

let current: AdminSession | null = null;
let ready = false;
const listeners = new Set<() => void>();

export function getSession(): AdminSession | null {
  return current;
}

export function isSessionReady(): boolean {
  return ready;
}

export function setSession(session: AdminSession | null): void {
  current = session;
  ready = true;
  writeStored(session);
  emit();
}

export function subscribeSession(listener: () => void): () => void {
  listeners.add(listener);
  if (!ready && typeof window !== "undefined") {
    queueMicrotask(restoreStored);
  }
  return () => listeners.delete(listener);
}

export function touchActivity(at = Date.now()): void {
  browserStorage()?.setItem(activityKey, String(at));
}

export function hasActivity(): boolean {
  return readActivity() !== null;
}

export function isIdle(now = Date.now()): boolean {
  const last = readActivity();
  return last !== null && now - last > idleLimitMs;
}

function readActivity(): number | null {
  const raw = browserStorage()?.getItem(activityKey);
  if (!raw) {
    return null;
  }

  const last = Number(raw);
  return Number.isFinite(last) ? last : null;
}

function emit(): void {
  listeners.forEach((listener) => listener());
}

function restoreStored(): void {
  if (ready) {
    return;
  }

  ready = true;
  current = readStored();
  emit();
}

function readStored(): AdminSession | null {
  const storage = browserStorage();
  if (!storage) {
    return null;
  }

  const raw = storage.getItem(storageKey);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as AdminSession;
    if (!parsed?.accessToken || !parsed.refreshToken || !parsed.user?.id) {
      storage.removeItem(storageKey);
      return null;
    }

    return parsed;
  } catch {
    storage.removeItem(storageKey);
    return null;
  }
}

function writeStored(session: AdminSession | null): void {
  const storage = browserStorage();
  if (!storage) {
    return;
  }

  if (!session) {
    storage.removeItem(storageKey);
    storage.removeItem(activityKey);
    return;
  }

  storage.setItem(storageKey, JSON.stringify(session));
}

function browserStorage(): Storage | null {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    return window.localStorage;
  } catch {
    return null;
  }
}
