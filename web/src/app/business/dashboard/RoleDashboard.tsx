import Link from "next/link";

export type MyDashboardWidget = {
  code: string;
  title: string;
  value: string;
  detail: string;
  href: string | null;
};

export type MyDashboardAction = {
  label: string;
  href: string;
  permission: string;
};

export type MyDashboard = {
  layout: string;
  displayName: string;
  widgets: MyDashboardWidget[];
  actions: MyDashboardAction[];
};

export function RoleDashboard({ dashboard }: { dashboard: MyDashboard }) {
  return (
    <div className="space-y-8">
      <header>
        <p className="text-sm text-muted">Your workspace</p>
        <h1 className="mt-1 text-3xl font-semibold">{dashboard.displayName ? `Hello, ${dashboard.displayName}` : "Dashboard"}</h1>
      </header>
      {dashboard.widgets.length === 0 ? (
        <section className="rounded-2xl border border-line bg-surface p-6">
          <h2 className="text-xl font-semibold">Nothing is assigned yet</h2>
          <p className="mt-2 text-muted">Your role does not have any dashboard widgets. An owner can turn them on under Roles &amp; Permissions.</p>
        </section>
      ) : (
        <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {dashboard.widgets.map((widget) => (
            <article className="rounded-2xl border border-line bg-surface p-5" key={widget.code}>
              <h2 className="text-sm text-muted">{widget.title}</h2>
              <p className="mt-3 text-3xl font-semibold">{widget.value}</p>
              <p className="mt-2 text-sm text-muted">{widget.detail}</p>
              {widget.href ? <Link className="mt-4 inline-flex min-h-11 items-center text-accent" href={widget.href}>Open</Link> : null}
            </article>
          ))}
        </section>
      )}
      {dashboard.actions.length > 0 ? (
        <section>
          <h2 className="text-lg font-semibold">Quick actions</h2>
          <div className="mt-3 flex flex-wrap gap-3">
            {dashboard.actions.map((action) => (
              <Link className="inline-flex min-h-11 items-center rounded-full border border-line px-4" href={action.href} key={action.href}>{action.label}</Link>
            ))}
          </div>
        </section>
      ) : null}
    </div>
  );
}
