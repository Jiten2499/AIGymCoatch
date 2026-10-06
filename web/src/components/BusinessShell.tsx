"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useSyncExternalStore } from "react";
import { getSession, setSession, subscribeSession } from "../lib/session";

const groups = [
  { title: "Overview", links: [["Dashboard", "/business"]] },
  { title: "Directory", links: [["Users", "/business/users"], ["Coaches", "/business/coaches"]] },
  { title: "Licensing", links: [["Plans", "/business/plans"]] },
] as const;

export function BusinessShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);

  useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [router, session]);

  if (!session) {
    return <p className="p-8 text-muted">Checking your session.</p>;
  }

  const workspace = session.currentTenant?.name ?? "Business";

  return (
    <div className="min-h-screen sm:grid sm:grid-cols-[220px_minmax(0,1fr)]">
      <aside className="border-b border-line p-4 sm:border-r sm:border-b-0">
        <p className="px-3 text-xs font-semibold tracking-[0.16em] text-accent uppercase">Business</p>
        <p className="mt-2 truncate px-3 text-sm">{workspace}</p>
        <nav aria-label="Business" className="mt-4 grid gap-4">
          {groups.map((group) => (
            <div key={group.title}>
              <p className="px-3 text-xs text-muted">{group.title}</p>
              {group.links.map(([label, href]) => {
                const current = href === "/business" ? pathname === href : pathname === href || pathname.startsWith(`${href}/`);
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
      <div className="min-w-0">
        <header className="flex items-center justify-between gap-4 border-b border-line px-6 py-4">
          <p className="text-sm text-muted">{workspace}</p>
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
