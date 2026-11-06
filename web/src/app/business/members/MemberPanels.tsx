"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { ScrollableGrid } from "../../../components/ScrollableGrid";
import { api, ApiError } from "../../../lib/api";
import type { GymMemberDetails, GymMembership } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { memberPaymentMethods } from "./memberWizard";

type Attendance = { id: string; checkInTime: string; checkOutTime: string | null; checkInMethod: string };
type Payment = { id: string; amount: number; currency: string; paymentMethod: string; paymentDate: string; status: string; referenceNumber: string | null };
type Summary = { packagePrice: number; discountAmount: number; finalAmount: number; currency: string; totalPaid: number; outstanding: number; settlementStatus: string };
type Measurement = { id: string; measurementDate: string; weight: number | null; bodyFatPercentage: number | null; waist: number | null; notes: string | null };

export function MemberPanels({
  canPay,
  canUpdate,
  member,
  memberId,
  tab,
  tenantId,
}: {
  canPay: boolean;
  canUpdate: boolean;
  member: GymMemberDetails;
  memberId: string;
  tab: "Overview" | "Membership" | "Payments" | "Attendance" | "Progress";
  tenantId: string | null;
}) {
  if (tab === "Overview") return <Overview canUpdate={canUpdate} member={member} memberId={memberId} tenantId={tenantId} />;
  if (tab === "Membership") return <Memberships memberId={memberId} tenantId={tenantId} />;
  if (tab === "Payments") return <Payments canPay={canPay} memberId={memberId} tenantId={tenantId} />;
  if (tab === "Attendance") return <AttendanceList memberId={memberId} tenantId={tenantId} />;
  return <Progress memberId={memberId} tenantId={tenantId} />;
}

function Overview({ canUpdate, member, memberId, tenantId }: { canUpdate: boolean; member: GymMemberDetails; memberId: string; tenantId: string | null }) {
  const client = useQueryClient();
  const status = useMutation({
    mutationFn: (next: string) => api(`/members/${memberId}/status`, { method: "PATCH", body: JSON.stringify({ status: next }) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["member", tenantId, memberId] }); },
  });
  return (
    <section className="mt-6 rounded-2xl border border-line bg-surface p-6">
      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <Field label="Email" value={member.email || "—"} />
        <Field label="Phone" value={member.phone || member.mobile || "—"} />
        <Field label="Joined" value={member.joinDate} />
        <Field label="Goal" value={member.fitnessGoal || "—"} />
        <Field label="Trainer" value={member.assignedTrainerName || "—"} />
        <Field label="City" value={[member.city, member.address].filter(Boolean).join(" · ") || "—"} />
      </dl>
      {status.error instanceof ApiError ? <p className="mt-3" role="alert">{status.error.message}</p> : null}
      {canUpdate ? (
        <button className="mt-4 min-h-11 rounded-full border border-line px-4" onClick={() => status.mutate(member.status === "Active" ? "Inactive" : "Active")} type="button">
          {member.status === "Active" ? "Mark inactive" : "Mark active"}
        </button>
      ) : null}
    </section>
  );
}

function Memberships({ memberId, tenantId }: { memberId: string; tenantId: string | null }) {
  const memberships = useQuery({
    queryKey: ["memberships", tenantId, memberId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<GymMembership>>(`/memberships?memberId=${memberId}&page=1&pageSize=20`),
  });
  return (
    <section className="mt-6 grid gap-3">
      {memberships.error instanceof ApiError ? <p role="alert">{memberships.error.message}</p> : null}
      {memberships.isLoading ? <p role="status">Loading memberships.</p> : null}
      {(memberships.data?.items ?? []).length === 0 && !memberships.isLoading ? <p className="text-muted">No membership is assigned.</p> : null}
      {(memberships.data?.items ?? []).map((item) => (
        <article className="rounded-2xl border border-line bg-surface p-4 text-sm" key={item.id}>
          <h2 className="font-semibold">{item.planName || item.membershipNumber}</h2>
          <p className="mt-1 text-muted">{item.startDate} to {item.endDate} · {item.status}</p>
          <p className="mt-1">{item.finalAmount ?? item.packagePrice ?? 0} {item.currency ?? ""} · Paid {item.totalPaid ?? 0} · Outstanding {item.outstanding ?? 0}</p>
          <p className="mt-1">Included PT sessions {item.ptSessionsRemaining ?? 0} of {item.includedPtSessions ?? 0} remaining</p>
        </article>
      ))}
    </section>
  );
}

function Payments({ canPay, memberId, tenantId }: { canPay: boolean; memberId: string; tenantId: string | null }) {
  const client = useQueryClient();
  const memberships = useQuery({
    queryKey: ["memberships", tenantId, memberId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<GymMembership>>(`/memberships?memberId=${memberId}&page=1&pageSize=20`),
  });
  const [membershipId, setMembershipId] = useState("");
  const selected = membershipId || memberships.data?.items[0]?.id || "";
  const payments = useQuery({
    queryKey: ["member-payments", tenantId, selected],
    enabled: tenantId !== null && selected !== "",
    queryFn: () => api<Page<Payment>>(`/memberships/${selected}/payments?page=1&pageSize=20`),
  });
  const summary = useQuery({
    queryKey: ["member-payment-summary", tenantId, selected],
    enabled: tenantId !== null && selected !== "",
    queryFn: () => api<Summary>(`/memberships/${selected}/payment-summary`),
  });
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState<(typeof memberPaymentMethods)[number]>("Cash");
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const record = useMutation({
    mutationFn: () => api(`/memberships/${selected}/payments`, { method: "POST", body: JSON.stringify({ amount: Number(amount), paymentMethod: method, paymentDate: date, referenceNumber: null, notes: null }) }),
    onSuccess: async () => {
      setAmount("");
      await client.invalidateQueries({ queryKey: ["member-payments", tenantId, selected] });
      await client.invalidateQueries({ queryKey: ["member-payment-summary", tenantId, selected] });
      await client.invalidateQueries({ queryKey: ["memberships", tenantId, memberId] });
    },
  });
  return (
    <section className="mt-6 grid gap-4">
      <label className="grid max-w-sm gap-2 text-sm">Membership
        <select aria-label="Payment membership" className="field" onChange={(event) => setMembershipId(event.target.value)} value={selected}>
          {(memberships.data?.items ?? []).map((item) => <option key={item.id} value={item.id}>{item.planName || item.membershipNumber}</option>)}
        </select>
      </label>
      {summary.data ? <p className="text-sm">Balance {summary.data.outstanding} {summary.data.currency} · {summary.data.settlementStatus} · Paid {summary.data.totalPaid} of {summary.data.finalAmount}</p> : null}
      {payments.error instanceof ApiError ? <p role="alert">{payments.error.message}</p> : null}
      <ScrollableGrid><ul className="grid w-max min-w-full gap-2 text-sm">
        {(payments.data?.items ?? []).map((item) => <li className="rounded-2xl border border-line bg-surface p-3" key={item.id}>{item.paymentDate} · {item.amount} {item.currency} · {item.paymentMethod} · {item.status}</li>)}
        {payments.data && payments.data.items.length === 0 ? <li className="text-muted">No payments recorded.</li> : null}
      </ul></ScrollableGrid>
      {canPay && selected ? (
        <form className="grid max-w-lg gap-3 rounded-2xl border border-line bg-surface p-4" onSubmit={(event) => { event.preventDefault(); record.mutate(); }}>
          <h2 className="font-semibold">Record payment</h2>
          <input aria-label="Payment amount" className="field" min="0" onChange={(event) => setAmount(event.target.value)} required step="0.01" type="number" value={amount} />
          <select aria-label="Payment method" className="field" onChange={(event) => setMethod(event.target.value as (typeof memberPaymentMethods)[number])} value={method}>
            {memberPaymentMethods.map((item) => <option key={item}>{item}</option>)}
          </select>
          <input aria-label="Payment date" className="field" onChange={(event) => setDate(event.target.value)} required type="date" value={date} />
          {record.error instanceof ApiError ? <p role="alert">{record.error.message}</p> : null}
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" type="submit">Save payment</button>
        </form>
      ) : null}
    </section>
  );
}

function AttendanceList({ memberId, tenantId }: { memberId: string; tenantId: string | null }) {
  const attendance = useQuery({
    queryKey: ["attendance", tenantId, memberId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<Attendance>>(`/attendance?memberProfileId=${memberId}&page=1&pageSize=20`),
  });
  return (
    <section className="mt-6">
      {attendance.error instanceof ApiError ? <p role="alert">{attendance.error.message}</p> : null}
      {attendance.isLoading ? <p role="status">Loading attendance.</p> : null}
      <ScrollableGrid><ul className="grid w-max min-w-full gap-2 text-sm">
        {(attendance.data?.items ?? []).map((item) => <li className="rounded-2xl border border-line bg-surface p-3" key={item.id}>{item.checkInTime} · {item.checkInMethod}{item.checkOutTime ? ` · out ${item.checkOutTime}` : ""}</li>)}
        {attendance.data && attendance.data.items.length === 0 ? <li className="text-muted">No attendance recorded.</li> : null}
      </ul></ScrollableGrid>
    </section>
  );
}

function Progress({ memberId, tenantId }: { memberId: string; tenantId: string | null }) {
  const progress = useQuery({
    queryKey: ["member-progress", tenantId, memberId],
    enabled: tenantId !== null,
    queryFn: () => api<Measurement[]>(`/members/${memberId}/progress`),
  });
  return (
    <section className="mt-6">
      {progress.error instanceof ApiError ? <p role="alert">{progress.error.message}</p> : null}
      {progress.isLoading ? <p role="status">Loading progress.</p> : null}
      <ScrollableGrid><ul className="grid w-max min-w-full gap-2 text-sm">
        {(progress.data ?? []).map((item) => <li className="rounded-2xl border border-line bg-surface p-3" key={item.id}>{item.measurementDate} · Weight {item.weight ?? "—"} · Body fat {item.bodyFatPercentage ?? "—"} · Waist {item.waist ?? "—"}</li>)}
        {progress.data && progress.data.length === 0 ? <li className="text-muted">No measurements yet. Progress appears after the member uses the app.</li> : null}
      </ul></ScrollableGrid>
    </section>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-muted">{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}
