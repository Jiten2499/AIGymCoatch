import { CoachKpiCard, CoachMetricIcon } from "./CoachKpiCard";

type CoachOverviewData = {
  email: string;
  specialization: string | null;
  joiningDate: string | null;
  employeeCode: string | null;
  assignedMembers: number;
  activePtClients: number;
  pendingApproval: number;
  approvedAmount: number;
};

export function CoachOverview({ coach }: { coach: CoachOverviewData }) {
  return (
    <div className="mt-5 grid gap-5 xl:grid-cols-[minmax(0,20rem)_minmax(0,1fr)]">
      <div className="grid content-start gap-4">
        <section className="rounded-2xl border border-line bg-background p-5">
          <h3 className="text-sm font-semibold tracking-wide text-muted uppercase">Trainer Information</h3>
          <dl className="mt-4 grid gap-4">
            <InfoRow label="Email" value={coach.email} />
            <InfoRow label="Specialization" value={coach.specialization || "—"} />
            <InfoRow label="Employee Code" value={coach.employeeCode || "—"} />
            <InfoRow label="Joining Date" value={coach.joiningDate || "—"} />
          </dl>
        </section>
        <section className="rounded-2xl border border-line bg-background p-5">
          <h3 className="text-sm font-semibold tracking-wide text-muted uppercase">Payout Summary</h3>
          <dl className="mt-4 grid gap-4">
            <InfoRow label="Pending Approval" value={formatAmount(coach.pendingApproval)} />
            <InfoRow label="Approved" value={formatAmount(coach.approvedAmount)} />
            <div>
              <dt className="text-sm text-muted">Paid</dt>
              <dd className="mt-2 rounded-xl border border-dashed border-line px-3 py-4 text-sm text-muted">No payout records</dd>
            </div>
          </dl>
        </section>
      </div>
      <section>
        <h3 className="mb-3 text-sm font-semibold tracking-wide text-muted uppercase">Snapshot</h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <CoachKpiCard icon={<CoachMetricIcon name="members" />} label="Assigned Members" note="All member assignments" value={String(coach.assignedMembers)} />
          <CoachKpiCard icon={<CoachMetricIcon name="active" />} label="Active PT Clients" note="Assignments still open" value={String(coach.activePtClients)} />
        </div>
        <p className="mt-4 text-sm text-muted">Detailed PT activity, trends and revenue live on the Performance tab. Targets and credits live on Target and Coach Credit.</p>
      </section>
    </div>
  );
}

export function CoachTabIcon({ name }: { name: string }) {
  const common = { "aria-hidden": true, className: "size-4 shrink-0", fill: "none", stroke: "currentColor", strokeLinecap: "round" as const, strokeLinejoin: "round" as const, strokeWidth: 1.75, viewBox: "0 0 24 24" };
  if (name === "overview") return <svg {...common}><rect height="7" rx="1.5" width="7" x="4" y="4" /><rect height="7" rx="1.5" width="7" x="13" y="4" /><rect height="7" rx="1.5" width="7" x="4" y="13" /><rect height="7" rx="1.5" width="7" x="13" y="13" /></svg>;
  if (name === "members") return <svg {...common}><path d="M16 20v-1.5a3.5 3.5 0 0 0-3.5-3.5h-5A3.5 3.5 0 0 0 4 18.5V20" /><circle cx="10" cy="8" r="3" /><path d="M20 20v-1.2A3.2 3.2 0 0 0 17.5 16" /><path d="M16 5.2a2.6 2.6 0 0 1 0 5" /></svg>;
  if (name === "performance") return <svg {...common}><path d="M4 19V5" /><path d="M4 19h16" /><path d="m7 14 3-3 3 2 4-5" /></svg>;
  if (name === "target") return <svg {...common}><circle cx="12" cy="12" r="8" /><circle cx="12" cy="12" r="4" /><circle cx="12" cy="12" r="1" /></svg>;
  if (name === "commission") return <svg {...common}><path d="M12 3v18" /><path d="M16.5 7.5A3.5 3.5 0 0 0 12 5H9.5a3 3 0 0 0 0 6H14a3 3 0 0 1 0 6H8" /></svg>;
  return <svg {...common}><path d="M8 7h12M8 12h12M8 17h12M4 7h.01M4 12h.01M4 17h.01" /></svg>;
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid gap-1 border-b border-line pb-3 last:border-0 last:pb-0">
      <dt className="text-sm text-muted">{label}</dt>
      <dd className="break-words text-sm font-medium">{value}</dd>
    </div>
  );
}

function formatAmount(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value);
}
