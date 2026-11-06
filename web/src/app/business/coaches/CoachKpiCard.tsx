export function CoachKpiCard({
  label,
  value,
  note,
  icon,
}: {
  label: string;
  value: string;
  note?: string;
  icon?: React.ReactNode;
}) {
  return (
    <article className="flex min-h-[7.5rem] flex-col justify-between rounded-2xl border border-line bg-background p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm text-muted">{label}</p>
          {note ? <p className="mt-1 text-xs text-muted">{note}</p> : null}
        </div>
        {icon ? (
          <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-xl border border-line bg-surface text-accent">
            {icon}
          </span>
        ) : null}
      </div>
      <p className="mt-4 text-2xl font-semibold tracking-tight">{value}</p>
    </article>
  );
}

export function CoachMetricIcon({ name }: { name: string }) {
  const common = { "aria-hidden": true, className: "size-4", fill: "none", stroke: "currentColor", strokeLinecap: "round" as const, strokeLinejoin: "round" as const, strokeWidth: 1.75, viewBox: "0 0 24 24" };
  if (name === "members" || name === "active" || name === "new") return <svg {...common}><path d="M16 20v-1.5a3.5 3.5 0 0 0-3.5-3.5h-5A3.5 3.5 0 0 0 4 18.5V20" /><circle cx="10" cy="8" r="3" /></svg>;
  if (name === "sessions") return <svg {...common}><circle cx="12" cy="12" r="8" /><path d="M12 8v4l2.5 1.5" /></svg>;
  if (name === "packages") return <svg {...common}><path d="M4 8 12 4l8 4-8 4Z" /><path d="M4 8v8l8 4 8-4V8" /></svg>;
  if (name === "target") return <svg {...common}><circle cx="12" cy="12" r="8" /><circle cx="12" cy="12" r="3" /></svg>;
  if (name === "revenue" || name === "commission" || name === "incentive") return <svg {...common}><path d="M12 3v18" /><path d="M16.5 7.5A3.5 3.5 0 0 0 12 5H9.5a3 3 0 0 0 0 6H14a3 3 0 0 1 0 6H8" /></svg>;
  return <svg {...common}><circle cx="12" cy="12" r="3" /></svg>;
}
