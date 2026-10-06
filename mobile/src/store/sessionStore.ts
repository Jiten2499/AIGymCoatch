import { create } from 'zustand';
import type { TenantSummary, UserProfile } from '../types/api';

export type SessionStatus = 'unknown' | 'anonymous' | 'authenticated';

type SessionState = {
  status: SessionStatus;
  accessToken: string | null;
  refreshToken: string | null;
  expiresAt: string | null;
  user: UserProfile | null;
  currentTenant: TenantSummary | null;
  setSession: (session: {
    accessToken: string;
    refreshToken: string;
    expiresAt: string;
    user: UserProfile;
    currentTenant: TenantSummary;
  }) => void;
  clearSession: () => void;
  markAnonymous: () => void;
};

export const useSessionStore = create<SessionState>((set) => ({
  status: 'unknown',
  accessToken: null,
  refreshToken: null,
  expiresAt: null,
  user: null,
  currentTenant: null,
  setSession: (session) => set({ status: 'authenticated', ...session }),
  clearSession: () =>
    set({
      status: 'anonymous',
      accessToken: null,
      refreshToken: null,
      expiresAt: null,
      user: null,
      currentTenant: null,
    }),
  markAnonymous: () => set({ status: 'anonymous' }),
}));
