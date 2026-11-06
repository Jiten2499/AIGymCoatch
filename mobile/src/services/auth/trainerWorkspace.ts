import type { WorkspaceSummary } from '../../types/api';

export function trainerWorkspaceToOpen(currentId: string | undefined, workspaces: readonly WorkspaceSummary[]): WorkspaceSummary | null {
  const current = workspaces.find((workspace) => workspace.id === currentId);
  if (current?.roles.includes('Trainer')) {
    return null;
  }
  return workspaces.find((workspace) => workspace.roles.includes('Trainer')) ?? null;
}
