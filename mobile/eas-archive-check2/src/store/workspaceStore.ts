import { create } from 'zustand';
import type { WorkspaceSummary } from '../types/api';

type WorkspaceState = {
  ready: boolean;
  workspaces: WorkspaceSummary[];
  logoUrl: string | null;
  features: Record<string, boolean>;
  setWorkspaces: (workspaces: WorkspaceSummary[]) => void;
  setLogoUrl: (tenantId: string, logoUrl: string | null) => void;
  setFeatures: (features: Record<string, boolean>) => void;
  reset: () => void;
};

export const useWorkspaceStore = create<WorkspaceState>((set, get) => ({
  ready: false,
  workspaces: [],
  logoUrl: null,
  features: {},
  setWorkspaces: (workspaces) => set({ workspaces, ready: true }),
  setLogoUrl: (tenantId, logoUrl) => {
    const current = get().workspaces.find((workspace) => workspace.id === tenantId);
    if (!current) {
      return;
    }
    set({ logoUrl });
  },
  setFeatures: (features) => set({ features }),
  reset: () => set({ ready: false, workspaces: [], logoUrl: null, features: {} }),
}));
