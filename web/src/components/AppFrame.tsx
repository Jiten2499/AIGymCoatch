"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { UserAvatar } from "./UserAvatar";

export type NavGroup = {
  title: string;
  links: readonly (readonly [string, string])[];
};

const storageKey = "lifefit.sidebar";

export function AppFrame({
  brand,
  subtitle,
  groups,
  user,
  profileHref,
  onLogout,
  children,
}: {
  brand: string;
  subtitle?: string;
  groups: readonly NavGroup[];
  user: { id: string; firstName: string; lastName: string; hasPhoto?: boolean };
  profileHref: string;
  onLogout: () => void;
  children: React.ReactNode;
}) {
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    const stored = localStorage.getItem(storageKey);
    if (stored === "collapsed" || stored === "expanded") {
      setCollapsed(stored === "collapsed");
      return;
    }
    setCollapsed(window.matchMedia("(max-width: 767px)").matches);
  }, []);

  function toggle() {
    setCollapsed((current) => {
      const next = !current;
      localStorage.setItem(storageKey, next ? "collapsed" : "expanded");
      return next;
    });
  }

  return (
    <div className="flex h-screen overflow-hidden bg-background">
      <aside className={`flex h-screen shrink-0 flex-col border-r border-line bg-background ${collapsed ? "w-[4.5rem]" : "w-60"}`}>
        <div className={`flex shrink-0 items-center gap-2 border-b border-line ${collapsed ? "justify-center px-2 py-4" : "px-4 py-4"}`}>
          {collapsed ? null : (
            <div className="min-w-0 flex-1">
              <p className="text-xs font-semibold tracking-[0.16em] text-accent uppercase">{brand}</p>
              {subtitle ? <p className="mt-1 truncate text-sm">{subtitle}</p> : null}
            </div>
          )}
          <button aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"} className="flex size-11 shrink-0 items-center justify-center rounded-xl border border-line" onClick={toggle} type="button">
            <Glyph name={collapsed ? "expand" : "collapse"} />
          </button>
        </div>
        <nav aria-label={brand} className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-2 py-3">
          {groups.map((group) => (
            <div className="mb-4" key={group.title}>
              {collapsed ? <div aria-hidden className="mx-3 mb-2 h-px bg-line" /> : <p className="px-3 pb-1 text-xs text-muted">{group.title}</p>}
              {group.links.map(([label, href]) => (
                <NavItem collapsed={collapsed} href={href} key={href} label={label} />
              ))}
            </div>
          ))}
        </nav>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-16 shrink-0 items-center justify-between gap-4 border-b border-line bg-background px-6">
          <p className="truncate text-sm text-muted">{subtitle || brand}</p>
          <div className="flex items-center gap-4">
            <Link className="flex min-h-11 items-center gap-2" href={profileHref}>
              <UserAvatar firstName={user.firstName} hasPhoto={user.hasPhoto} lastName={user.lastName} userId={user.id} />
              <span className="truncate">{`${user.firstName} ${user.lastName}`.trim()}</span>
            </Link>
            <button className="min-h-11 rounded-full border border-line px-4" onClick={onLogout} type="button">Log out</button>
          </div>
        </header>
        <main className="min-h-0 flex-1 overflow-y-auto px-6 py-8">{children}</main>
      </div>
    </div>
  );
}

function NavItem({ collapsed, href, label }: { collapsed: boolean; href: string; label: string }) {
  const pathname = usePathname();
  const current = href === "/business" || href === "/admin" ? pathname === href : pathname === href || pathname.startsWith(`${href}/`);
  const [tipTop, setTipTop] = useState<number | null>(null);

  return (
    <Link
      aria-label={label}
      className={`relative flex min-h-11 items-center gap-3 rounded-xl px-3 ${collapsed ? "justify-center" : ""} ${current ? "bg-surface text-accent" : ""}`}
      href={href}
      onMouseEnter={(event) => {
        if (!collapsed) return;
        const box = event.currentTarget.getBoundingClientRect();
        setTipTop(box.top + box.height / 2);
      }}
      onMouseLeave={() => setTipTop(null)}
    >
      <Glyph name={iconFor(href)} />
      {collapsed ? null : <span className="truncate">{label}</span>}
      {collapsed && tipTop !== null ? (
        <span className="pointer-events-none fixed z-50 -translate-y-1/2 rounded-lg border border-line bg-surface px-3 py-2 text-sm whitespace-nowrap text-foreground shadow" style={{ left: "4.75rem", top: tipTop }}>
          {label}
        </span>
      ) : null}
    </Link>
  );
}

function iconFor(href: string) {
  if (href === "/business" || href === "/admin") return "home";
  if (href.includes("users") || href.includes("members") || href.includes("coaches") || href.includes("assignments")) return "people";
  if (href.includes("membership")) return "card";
  if (href.includes("lead")) return "flag";
  if (href.includes("expense")) return "receipt";
  if (href.includes("ledger")) return "ledger";
  if (href.includes("communication")) return "megaphone";
  if (href.includes("integration") || href.includes("llm")) return "plug";
  if (href.includes("configuration") || href.includes("models")) return "sliders";
  if (href.includes("plan") || href.includes("licen") || href.includes("feature") || href.includes("expir")) return "badge";
  if (href.includes("tenant")) return "building";
  if (href.includes("audit") || href.includes("job")) return "list";
  return "dot";
}

function Glyph({ name }: { name: string }) {
  const common = { "aria-hidden": true, className: "size-5 shrink-0", fill: "none", stroke: "currentColor", strokeLinecap: "round" as const, strokeLinejoin: "round" as const, strokeWidth: 1.75, viewBox: "0 0 24 24" };
  if (name === "collapse") return <svg {...common}><path d="M15 6 9 12l6 6" /></svg>;
  if (name === "expand") return <svg {...common}><path d="m9 6 6 6-6 6" /></svg>;
  if (name === "home") return <svg {...common}><path d="M4 10.5 12 4l8 6.5V20H4Z" /></svg>;
  if (name === "people") return <svg {...common}><path d="M16 20v-1.5a3.5 3.5 0 0 0-3.5-3.5h-5A3.5 3.5 0 0 0 4 18.5V20" /><circle cx="10" cy="8" r="3" /><path d="M20 20v-1.2A3.2 3.2 0 0 0 17.5 16" /><path d="M16 5.2a2.6 2.6 0 0 1 0 5" /></svg>;
  if (name === "card") return <svg {...common}><rect height="14" rx="2" width="18" x="3" y="5" /><path d="M3 10h18" /></svg>;
  if (name === "flag") return <svg {...common}><path d="M5 21V4" /><path d="M5 4h11l-1.5 4L16 12H5" /></svg>;
  if (name === "receipt") return <svg {...common}><path d="M7 3h10v18l-2.5-1.5L12 21l-2.5-1.5L7 21Z" /><path d="M9 8h6M9 12h6" /></svg>;
  if (name === "ledger") return <svg {...common}><path d="M5 4h14v16H5Z" /><path d="M9 8h6M9 12h6M9 16h4" /></svg>;
  if (name === "megaphone") return <svg {...common}><path d="M4 10v4h3l8 4V6L7 10Z" /><path d="M18 9.5a3.5 3.5 0 0 1 0 5" /></svg>;
  if (name === "plug") return <svg {...common}><path d="M9 7V3M15 7V3M8 7h8v5a4 4 0 0 1-8 0Z" /><path d="M12 16v5" /></svg>;
  if (name === "sliders") return <svg {...common}><path d="M4 7h16M4 12h16M4 17h16" /><circle cx="9" cy="7" r="2" /><circle cx="15" cy="12" r="2" /><circle cx="8" cy="17" r="2" /></svg>;
  if (name === "badge") return <svg {...common}><circle cx="12" cy="12" r="8" /><path d="m9 12 2 2 4-4" /></svg>;
  if (name === "building") return <svg {...common}><path d="M4 20h16M6 20V5h8v15M14 9h4v11" /><path d="M8 8h2M8 12h2M8 16h2" /></svg>;
  if (name === "list") return <svg {...common}><path d="M8 7h12M8 12h12M8 17h12M4 7h.01M4 12h.01M4 17h.01" /></svg>;
  return <svg {...common}><circle cx="12" cy="12" r="3" /></svg>;
}
