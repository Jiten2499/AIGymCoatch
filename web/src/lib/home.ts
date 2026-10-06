import { api } from "./api";
import { getSession, setSession, type AdminSession, type WorkspaceSummary } from "./session";

const businessRoles = ["GymOwner", "GymManager", "Trainer", "Receptionist"];

export type HomeDestination = {
  href: "/admin" | "/business";
  tenantId: string | null;
};

export function resolveHome(workspaces: WorkspaceSummary[]): HomeDestination {
  if (workspaces.some((workspace) => workspace.type === "Platform" && workspace.roles.includes("SuperAdmin"))) {
    return { href: "/admin", tenantId: null };
  }

  const business = workspaces.find(
    (workspace) => workspace.type !== "Platform" && workspace.status === "Active" && workspace.roles.some((role) => businessRoles.includes(role)),
  );
  return { href: "/business", tenantId: business?.id ?? null };
}

export async function enterWorkspace(): Promise<HomeDestination["href"]> {
  const workspaces = await api<WorkspaceSummary[]>("/tenants/me");
  const decision = resolveHome(workspaces);
  const currentId = getSession()?.currentTenant?.id;
  if (decision.tenantId && decision.tenantId !== currentId) {
    const next = await api<AdminSession>("/tenants/switch", {
      method: "POST",
      body: JSON.stringify({ tenantId: decision.tenantId }),
    });
    setSession(next);
  }

  return decision.href;
}
