export type AdminUser = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
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

let current: AdminSession | null = null;
const listeners = new Set<() => void>();

export function getSession(): AdminSession | null {
  return current;
}

export function setSession(session: AdminSession | null): void {
  current = session;
  listeners.forEach((listener) => listener());
}

export function subscribeSession(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
