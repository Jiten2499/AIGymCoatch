"use client";

import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { AppFrame, type NavGroup } from "./AppFrame";
import { api } from "../lib/api";
import { canSeeBusinessLink, gymLinks } from "../lib/businessNav";
import { enterWorkspace } from "../lib/home";
import { useCircularRealtime } from "../lib/circularRealtime";
import { getSession, isSessionReady, setSession, subscribeSession, type WorkspaceSummary } from "../lib/session";
import { displayWorkspaceRole, workspaceRoleLabel } from "../lib/workspaceRole";

export function BusinessShell({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const sessionReady = useSyncExternalStore(subscribeSession, isSessionReady, () => false);
  const [workspaceSettled, setWorkspaceSettled] = useState(false);
  const onPlatform = !session?.currentTenant || session.currentTenant.type === "Platform";
  const signedIn = session !== null;
  const tenantId = onPlatform ? null : session?.currentTenant?.id ?? null;
  useCircularRealtime(tenantId);
  const features = useQuery({
    queryKey: ["features", tenantId],
    enabled: tenantId !== null,
    staleTime: 0,
    refetchOnMount: "always",
    queryFn: () => api<{ featureCode: string; enabled: boolean }[]>("/features"),
  });
  const access = useQuery({
    queryKey: ["my-access", tenantId],
    enabled: tenantId !== null,
    staleTime: 0,
    retry: false,
    refetchOnMount: "always",
    queryFn: () => api<{ permissions: string[] }>("/users/me/access"),
  });
  const unread = useQuery({
    queryKey: ["communication-unread", tenantId],
    enabled: tenantId !== null,
    retry: false,
    queryFn: () => api<number>("/communications/inbox/unread-count"),
  });
  const approvals = useQuery({
    queryKey: ["approval-count", tenantId],
    enabled: tenantId !== null,
    retry: false,
    queryFn: () => api<{ pendingCount: number }>("/approval-requests/pending-count"),
  });
  const workspaces = useQuery({
    queryKey: ["workspaces", session?.user.id],
    enabled: session !== null,
    staleTime: 60_000,
    queryFn: () => api<WorkspaceSummary[]>("/tenants/me"),
  });
  const permissions = useMemo(() => {
    const payload = access.data as { permissions?: string[]; Permissions?: string[] } | undefined;
    const codes = payload?.permissions ?? payload?.Permissions;
    return access.isSuccess && Array.isArray(codes) ? new Set(codes) : null;
  }, [access.data, access.isSuccess]);
  const enabled = useMemo(() => new Map((features.data ?? []).map((feature) => [feature.featureCode, feature.enabled])), [features.data]);
  const visibleGymLinks = gymLinks.filter(([, href]) => canSeeBusinessLink(href, permissions, enabled));
  const taskLabel = approvals.isSuccess && approvals.data.pendingCount > 0 ? `Tasks (${approvals.data.pendingCount})` : "Tasks";
  const circularsLabel = unread.isSuccess && unread.data > 0 ? `Circulars (${unread.data})` : "Circulars";
  const labeledLinks = visibleGymLinks.map(([label, href]) => [href === "/business/communications" ? circularsLabel : label, href] as [string, string]);
  const gymNavigation: [string, string][] = approvals.isSuccess
    ? [["Members", "/business/members"], ["Memberships", "/business/memberships"], [taskLabel, "/business/tasks"], ...labeledLinks.filter(([, href]) => href !== "/business/members" && href !== "/business/memberships")]
    : labeledLinks;
  const allow = (href: string) => canSeeBusinessLink(href, permissions, enabled);
  const groups: NavGroup[] = [
    { title: "Overview", links: allow("/business") ? ([["Dashboard", "/business"]] as const) : [] },
    { title: "Access", links: ([["Staff & Access", "/business/users"], ["Coaches", "/business/coaches"]] as const).filter(([, href]) => allow(href)) },
    { title: "Gym", links: gymNavigation.filter(([, href]) => allow(href)) },
    { title: "Licensing", links: allow("/business/plans") ? ([["Plans", "/business/plans"]] as const) : [] },
  ].filter((group) => group.links.length > 0);

  useEffect(() => {
    if (sessionReady && !signedIn) {
      router.replace("/login");
    }
  }, [router, sessionReady, signedIn]);

  useEffect(() => {
    if (!signedIn || !onPlatform) {
      return;
    }

    let cancelled = false;
    void enterWorkspace()
      .then((href) => {
        if (!cancelled && href !== "/business") {
          router.replace(href);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setWorkspaceSettled(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [onPlatform, router, signedIn]);

  if (!session) {
    return <p className="p-8 text-muted">Checking your session.</p>;
  }

  if (onPlatform && !workspaceSettled) {
    return <p className="p-8 text-muted">Opening your workspace.</p>;
  }

  const currentRoles = workspaces.data?.find((workspace) => workspace.id === tenantId)?.roles ?? [];
  const role = workspaceRoleLabel(displayWorkspaceRole(currentRoles)) || "Staff";

  return (
    <AppFrame
      brand="Business"
      groups={groups}
      onLogout={() => { setSession(null); router.replace("/login"); }}
      profileHref="/business/profile"
      subtitle={role}
      user={session.user}
    >
      {children}
    </AppFrame>
  );
}
