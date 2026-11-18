"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect, useSyncExternalStore } from "react";
import { AppFrame } from "./AppFrame";
import { adminGroups } from "../lib/adminNav";
import { api } from "../lib/api";
import { getSession, isSessionReady, setSession, subscribeSession, type WorkspaceSummary } from "../lib/session";
import { displayWorkspaceRole, workspaceRoleLabel } from "../lib/workspaceRole";

export function AdminShell({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const sessionReady = useSyncExternalStore(subscribeSession, isSessionReady, () => false);
  const userId = session?.user.id ?? null;
  const tenantType = session?.currentTenant?.type ?? null;
  const jobs = useQuery({
    queryKey: ["background-jobs", "access"],
    enabled: session !== null && session.currentTenant?.type === "Platform",
    retry: false,
    queryFn: () => api("/background-jobs/summary"),
  });
  const canMonitorJobs = jobs.isSuccess;
  const workspaces = useQuery({
    queryKey: ["workspaces"],
    enabled: session !== null && tenantType === "Platform",
    queryFn: () => api<WorkspaceSummary[]>("/tenants/me"),
  });
  const roleName = displayWorkspaceRole(workspaces.data?.find((workspace) => workspace.type === "Platform")?.roles ?? []);
  const role = roleName ? workspaceRoleLabel(roleName) : "";

  useEffect(() => {
    if (!sessionReady) {
      return;
    }

    if (!userId) {
      router.replace("/login");
      return;
    }

    if (tenantType && tenantType !== "Platform") {
      router.replace("/business");
    }
  }, [router, sessionReady, tenantType, userId]);

  if (!session) {
    return <p className="p-8 text-muted">Checking your session.</p>;
  }

  return (
    <AppFrame
      brand="Growventur"
      role={role}
      groups={adminGroups(canMonitorJobs)}
      onLogout={() => { setSession(null); router.replace("/login"); }}
      profileHref="/admin/profile"
      user={session.user}
    >
      {children}
    </AppFrame>
  );
}

export function PageState({ loading, error, onRetry, children }: { loading: boolean; error: string | null; onRetry: () => void; children: React.ReactNode }) {
  if (loading) {
    return <p role="status">Loading platform data.</p>;
  }
  if (error) {
    return (
      <div className="rounded-2xl border border-line bg-surface p-6" role="alert">
        <p>{error}</p>
        <button className="mt-4 min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={onRetry} type="button">
          Try again
        </button>
      </div>
    );
  }
  return children;
}
