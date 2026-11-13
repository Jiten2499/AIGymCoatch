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
type PtBalance = { id: string; packageName: string; price: number; currency: string; amountPaid?: number; status: string };
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

const paymentMethodLabels: Record<(typeof memberPaymentMethods)[number], string> = {
  Cash: "Cash",
  Upi: "UPI",
  DebitCard: "Debit card",
  CreditCard: "Credit card",
  BankTransfer: "Bank transfer",
  OnlinePayment: "Online payment",
  Cheque: "Cheque",
  Other: "Other",
};

const referenceMethods = new Set<(typeof memberPaymentMethods)[number]>(["Upi", "DebitCard", "CreditCard", "BankTransfer", "OnlinePayment", "Cheque"]);

function membershipLabel(item: GymMembership) {
  return `Membership · ${item.planName || item.membershipNumber} · ${item.startDate} to ${item.endDate} · ${item.status}`;
}

function pendingPt(item: PtBalance) {
  return item.status === "Cancelled" ? 0 : Math.max(0, item.price - (item.amountPaid ?? 0));
}

function Payments({ canPay, memberId, tenantId }: { canPay: boolean; memberId: string; tenantId: string | null }) {
  const client = useQueryClient();
  const memberships = useQuery({
    queryKey: ["memberships", tenantId, memberId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<GymMembership>>(`/memberships?memberId=${memberId}&page=1&pageSize=20`),
  });
  const packages = useQuery({
    queryKey: ["member-pt", tenantId, memberId],
    enabled: tenantId !== null,
    queryFn: () => api<PtBalance[]>(`/members/${memberId}/pt-assignments`),
  });
  const options = [
    ...(memberships.data?.items ?? []).map((item) => ({ key: `membership:${item.id}`, label: membershipLabel(item) })),
    ...(packages.data ?? []).flatMap((item) => {
      const due = pendingPt(item);
      return due > 0 ? [{ key: `pt:${item.id}`, label: `PT · ${item.packageName} · pending ${due} ${item.currency}` }] : [];
    }),
  ];
  const [target, setTarget] = useState("");
  const selected = options.some((item) => item.key === target) ? target : options[0]?.key ?? "";
  const membershipId = selected.startsWith("membership:") ? selected.slice("membership:".length) : "";
  const ptId = selected.startsWith("pt:") ? selected.slice(3) : "";
  const pt = (packages.data ?? []).find((item) => item.id === ptId) ?? null;
  const payments = useQuery({
    queryKey: ["member-payments", tenantId, selected],
    enabled: tenantId !== null && membershipId !== "",
    queryFn: () => api<Page<Payment>>(`/memberships/${membershipId}/payments?page=1&pageSize=20`),
  });
  const ptPayments = useQuery({
    queryKey: ["member-pt-payments", tenantId, ptId],
    enabled: tenantId !== null && ptId !== "",
    queryFn: () => api<Payment[]>(`/members/${memberId}/pt-assignments/${ptId}/payments`),
  });
  const summary = useQuery({
    queryKey: ["member-payment-summary", tenantId, membershipId],
    enabled: tenantId !== null && membershipId !== "",
    queryFn: () => api<Summary>(`/memberships/${membershipId}/payment-summary`),
  });
  const paymentRows = membershipId ? payments.data?.items ?? [] : ptPayments.data ?? [];
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState<(typeof memberPaymentMethods)[number]>("Cash");
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [reference, setReference] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const record = useMutation({
    mutationFn: () => api(membershipId ? `/memberships/${membershipId}/payments` : `/members/${memberId}/pt-assignments/${ptId}/payments`, { method: "POST", body: JSON.stringify({ amount: Number(amount), paymentMethod: method, paymentDate: date, referenceNumber: reference.trim() || null, notes: null }) }),
    onSuccess: async () => {
      setAmount("");
      setReference("");
      setFormError(null);
      await client.invalidateQueries({ queryKey: ["member-payments", tenantId] });
      await client.invalidateQueries({ queryKey: ["member-pt-payments", tenantId] });
      await client.invalidateQueries({ queryKey: ["member-payment-summary", tenantId] });
      await client.invalidateQueries({ queryKey: ["memberships", tenantId, memberId] });
      await client.invalidateQueries({ queryKey: ["member-pt", tenantId, memberId] });
      await client.invalidateQueries({ queryKey: ["ledger-summary", tenantId] });
      await client.invalidateQueries({ queryKey: ["ledger-transactions", tenantId] });
    },
  });
  const ptDue = pt ? pendingPt(pt) : 0;
  const loading = memberships.isLoading || packages.isLoading;
  return (
    <section className="mt-6 grid gap-4">
      <p className="max-w-2xl text-sm text-muted">Collect the pending balance for a gym membership or a personal-training package. The balance is the price minus payments already recorded.</p>
      {loading ? <p role="status">Loading balances.</p> : null}
      {memberships.error instanceof ApiError ? <p role="alert">{memberships.error.message}</p> : null}
      {packages.error instanceof ApiError ? <p role="alert">{packages.error.message}</p> : null}
      {!loading && options.length === 0 ? <p className="text-muted">No membership or personal-training package has a pending balance.</p> : null}
      {options.length > 0 ? (
        <div className="grid max-w-xl gap-2 text-sm">
          <span id="payment-membership-label">Balance</span>
          <select aria-labelledby="payment-membership-label" className="field" onChange={(event) => setTarget(event.target.value)} value={selected}>
            {options.map((item) => <option key={item.key} value={item.key}>{item.label}</option>)}
          </select>
        </div>
      ) : null}
      {summary.data ? <p className="text-sm">Balance {summary.data.outstanding} {summary.data.currency} · {summary.data.settlementStatus} · Paid {summary.data.totalPaid} of {summary.data.finalAmount}</p> : null}
      {pt ? <p className="text-sm">Balance {ptDue} {pt.currency} · Paid {pt.amountPaid ?? 0} of {pt.price}</p> : null}
      {payments.error instanceof ApiError ? <p role="alert">{payments.error.message}</p> : null}
      {ptPayments.error instanceof ApiError ? <p role="alert">{ptPayments.error.message}</p> : null}
      <ScrollableGrid><ul className="grid w-max min-w-full gap-2 text-sm">
        {paymentRows.map((item) => <li className="rounded-2xl border border-line bg-surface p-3" key={item.id}>{item.paymentDate} · {item.amount} {item.currency} · {paymentMethodLabels[item.paymentMethod as (typeof memberPaymentMethods)[number]] ?? item.paymentMethod} · {item.status}{item.referenceNumber ? ` · ${item.referenceNumber}` : ""}</li>)}
        {(membershipId ? payments.data : ptPayments.data) && paymentRows.length === 0 ? <li className="text-muted">No payments recorded.</li> : null}
      </ul></ScrollableGrid>
      {canPay && selected ? (
        <form className="grid max-w-lg gap-3 rounded-2xl border border-line bg-surface p-4" onSubmit={(event) => { event.preventDefault(); if (referenceMethods.has(method) && !reference.trim()) { setFormError("Enter a reference number for this payment method."); return; } setFormError(null); record.mutate(); }}>
          <h2 className="font-semibold">Record payment</h2>
          <input aria-label="Payment amount" className="field" max={pt ? ptDue : summary.data?.outstanding} min="0.01" onChange={(event) => setAmount(event.target.value)} required step="0.01" type="number" value={amount} />
          <div className="grid gap-2 text-sm">
            <span id="payment-method-label">Payment method</span>
            <select aria-labelledby="payment-method-label" className="field" onChange={(event) => setMethod(event.target.value as (typeof memberPaymentMethods)[number])} value={method}>
              {memberPaymentMethods.map((item) => <option key={item} value={item}>{paymentMethodLabels[item]}</option>)}
            </select>
          </div>
          <input aria-label="Payment date" className="field" onChange={(event) => setDate(event.target.value)} required type="date" value={date} />
          <input aria-label="Reference number" className="field" onChange={(event) => setReference(event.target.value)} placeholder={referenceMethods.has(method) ? "Required for this method" : "Optional"} value={reference} />
          {formError ? <p role="alert">{formError}</p> : null}
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
