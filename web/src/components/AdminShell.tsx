"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState, useSyncExternalStore } from "react";
import { api } from "../lib/api";
import { getSession, setSession, subscribeSession } from "../lib/session";

const groups = [
  { title: "Overview", links: [["Overview", "/admin"]] },
  { title: "AI", links: [["Connections", "/admin/llm"], ["Models", "/admin/models"]] },
  { title: "Directory", links: [["Tenants", "/admin/tenants"], ["Users", "/admin/users"]] },
  { title: "Licensing", links: [["Plans", "/admin/plans"], ["Features", "/admin/features"], ["Subscriptions", "/admin/licenses"], ["Seats", "/admin/assignments"], ["Expiring", "/admin/expiring"]] },
  { title: "Audit", links: [["Activity", "/admin/audit"]] },
] as const;

export function AdminShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const [platform, setPlatform] = useState<string>("");

  useEffect(() => {
    if (!session) {
      router.replace("/login");
      return;
    }

    if (session.currentTenant && session.currentTenant.type !== "Platform") {
      router.replace("/business");
    }
  }, [router, session]);

  useEffect(() => {
    if (!session) {
      return;
    }
    void api<{ name: string; version: string; status: string }>("/platform").then((info) => setPlatform(`${info.name} ${info.version}`)).catch(() => setPlatform(""));
  }, [session]);

  if (!session) {
    return <p className="p-8 text-muted">Checking your session.</p>;
  }

  return (
    <div className="min-h-screen md:grid md:grid-cols-[240px_1fr]">
      <aside className="border-b border-line p-4 md:border-r md:border-b-0">
        <p className="px-3 text-xs font-semibold tracking-[0.16em] text-accent uppercase">Platform</p>
        <nav aria-label="Platform administration" className="mt-4 grid gap-4">
          {groups.map((group) => (
            <div key={group.title}>
              <p className="px-3 text-xs text-muted">{group.title}</p>
              {group.links.map(([label, href]) => {
                const current = href === "/admin" ? pathname === href : pathname === href || pathname.startsWith(`${href}/`);
                return (
                  <Link className={`flex min-h-11 items-center rounded-xl px-3 ${current ? "bg-surface text-accent" : ""}`} href={href} key={href}>
                    {label}
                  </Link>
                );
              })}
            </div>
          ))}
        </nav>
      </aside>
      <div>
        <header className="flex items-center justify-between gap-4 border-b border-line px-6 py-4">
          <p className="text-sm text-muted">{platform}</p>
          <div className="flex items-center gap-4">
            <p>{session.user.firstName} {session.user.lastName}</p>
            <button className="min-h-11 rounded-full border border-line px-4" onClick={() => { setSession(null); router.replace("/login"); }} type="button">
              Log out
            </button>
          </div>
        </header>
        <main className="px-6 py-8">{children}</main>
      </div>
    </div>
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
