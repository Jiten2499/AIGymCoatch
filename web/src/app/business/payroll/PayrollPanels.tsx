"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { ApiError } from "../../../lib/api";
import {
  addAdjustment,
  getPayslip,
  listCompensation,
  money,
  periodLabel,
  recordPayment,
  saveCompensation,
  type PayrollLine,
  type PayrollRunDetails,
  type PayrollStep,
} from "../../../lib/payroll";

export function issueText(line: PayrollLine): string {
  if (line.missingSalary) return "Missing salary details";
  if (line.unapprovedCommission) return "Unapproved commission";
  if (line.unconfirmedAttendance) return "Unconfirmed attendance";
  return "Needs review";
}

export function SummaryCards({ details, onChangePeriod }: { details: PayrollRunDetails; onChangePeriod: () => void }) {
  return (
    <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <article className="rounded-2xl border border-line bg-surface px-4 py-4 shadow-sm">
        <p className="text-sm text-muted">Pay period</p>
        <p className="mt-2 text-xl font-semibold">{periodLabel(details.periodYear, details.periodMonth)}</p>
        <button className="mt-2 text-sm text-accent" onClick={onChangePeriod} type="button">Change</button>
      </article>
      <article className="rounded-2xl border border-line bg-surface px-4 py-4 shadow-sm">
        <p className="text-sm text-muted">Staff included</p>
        <p className="mt-2 text-xl font-semibold">{details.staffCount}</p>
      </article>
      <article className="rounded-2xl border border-line bg-surface px-4 py-4 shadow-sm">
        <p className="text-sm text-muted">Total net payable</p>
        <p className="mt-2 text-xl font-semibold">{money(details.netPayable, details.currency)}</p>
        <p className="mt-1 text-xs text-muted">{money(details.grossPay, details.currency)} gross − {money(details.totalDeductions, details.currency)} deductions</p>
      </article>
      <article className={`rounded-2xl border px-4 py-4 shadow-sm ${details.issueCount > 0 ? "border-amber-500/40 bg-amber-500/10" : "border-line bg-surface"}`}>
        <p className="text-sm text-muted">Needs review</p>
        <p className="mt-2 text-xl font-semibold">{details.issueCount} staff</p>
      </article>
    </section>
  );
}

export function ActionBar(props: { step: PayrollStep; details: PayrollRunDetails; onSubmit: () => void; onDraft: () => void; onApprove: () => void; onBulk: () => void; pending: boolean }) {
  const blocked = props.details.issueCount > 0;
  return (
    <div className="flex flex-wrap gap-2">
      {props.details.canEdit && props.step === "prepare" ? <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={props.pending || blocked} onClick={props.onSubmit} type="button">Proceed to review →</button> : null}
      {props.details.canEdit && props.step === "review" ? (
        <>
          <button className="min-h-11 rounded-full border border-line px-4" disabled={props.pending} onClick={props.onDraft} type="button">Back to prepare</button>
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={props.pending || blocked || !props.details.canApprove} onClick={props.onApprove} type="button">Approve payroll</button>
        </>
      ) : null}
      {props.step === "approve" && props.details.canApprove ? <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={props.pending || blocked} onClick={props.onApprove} type="button">Confirm approval</button> : null}
      {props.details.canPay && props.details.amountPaid < props.details.netPayable ? <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={props.pending} onClick={props.onBulk} type="button">Record remaining payments</button> : null}
    </div>
  );
}

export function DetailPanel({ line, currency, canEdit, canPay, details, onChanged }: { line: PayrollLine | null; currency: string; canEdit: boolean; canPay: boolean; details: PayrollRunDetails; onChanged: () => Promise<void> }) {
  const [kind, setKind] = useState<"Bonus" | "Deduction">("Bonus");
  const [label, setLabel] = useState("");
  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("");
  const [payAmount, setPayAmount] = useState("");
  const [payRef, setPayRef] = useState("");
  const adjust = useMutation({ mutationFn: () => addAdjustment(line!.id, kind, label, Number(amount), reason), onSuccess: async () => { setLabel(""); setAmount(""); setReason(""); await onChanged(); } });
  const pay = useMutation({ mutationFn: () => recordPayment(line!.id, Number(payAmount), new Date().toISOString().slice(0, 10), "BankTransfer", payRef || null), onSuccess: async () => { setPayAmount(""); setPayRef(""); await onChanged(); } });
  const slip = useMutation({ mutationFn: () => getPayslip(line!.id) });

  return (
    <aside className="grid gap-4 self-start">
      <section className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
        <h2 className="text-lg font-semibold">Pay period summary</h2>
        <dl className="mt-4 grid gap-2 text-sm">
          <Row label="Staff" value={String(details.staffCount)} />
          <Row label="Gross pay" value={money(details.grossPay, currency)} />
          <Row label="Deductions" value={money(details.totalDeductions, currency)} />
          <Row label="Net payable" value={money(details.netPayable, currency)} />
          <Row label="Paid" value={money(details.amountPaid, currency)} />
        </dl>
      </section>
      {!line ? (
        <section className="rounded-2xl border border-line bg-surface p-5 text-sm text-muted">Select a staff member to see pay details.</section>
      ) : (
        <section className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
          <h2 className="text-lg font-semibold">{line.employeeName}</h2>
          <p className="text-sm text-muted">{line.employeeCode} · {line.staffType}</p>
          <dl className="mt-4 grid gap-2 text-sm">
            <Row label="Base salary" value={money(line.baseSalary, currency)} />
            <Row label="Allowances" value={money(line.allowances, currency)} />
            <Row label="Commissions" value={money(line.commissions, currency)} />
            <Row label="Bonuses" value={money(line.bonuses, currency)} />
            <Row label="Deductions" value={money(line.deductions, currency)} />
            <Row label="Gross" value={money(line.grossPay, currency)} />
            <Row label="Net" value={money(line.netPay, currency)} />
            <Row label="Paid" value={`${money(line.amountPaid, currency)} · ${line.paymentStatus}`} />
          </dl>
          {(line.missingSalary || line.unapprovedCommission || line.unconfirmedAttendance) ? (
            <ul className="mt-3 grid gap-1 text-sm text-muted">
              {line.missingSalary ? <li>Missing salary details</li> : null}
              {line.unapprovedCommission ? <li>Unapproved commission</li> : null}
              {line.unconfirmedAttendance ? <li>Unconfirmed attendance</li> : null}
            </ul>
          ) : null}
          {canEdit ? (
            <form className="mt-4 grid gap-2" onSubmit={(event) => { event.preventDefault(); adjust.mutate(); }}>
              <h3 className="font-semibold">Add bonus or deduction</h3>
              <select aria-label="Adjustment kind" className="field" onChange={(event) => setKind(event.target.value as "Bonus" | "Deduction")} value={kind}><option>Bonus</option><option>Deduction</option></select>
              <input aria-label="Label" className="field" onChange={(event) => setLabel(event.target.value)} placeholder="Label" required value={label} />
              <input aria-label="Amount" className="field" onChange={(event) => setAmount(event.target.value)} placeholder="Amount" required value={amount} />
              <input aria-label="Reason" className="field" onChange={(event) => setReason(event.target.value)} placeholder="Reason" required value={reason} />
              {adjust.error instanceof ApiError ? <p role="alert">{adjust.error.message}</p> : null}
              <button className="min-h-11 rounded-full border border-line px-4" disabled={adjust.isPending} type="submit">Save adjustment</button>
            </form>
          ) : null}
          {canPay && line.remaining > 0 ? (
            <form className="mt-4 grid gap-2" onSubmit={(event) => { event.preventDefault(); pay.mutate(); }}>
              <h3 className="font-semibold">Record payment</h3>
              <p className="text-xs text-muted">Records payment status only. No bank transfer is initiated.</p>
              <input aria-label="Payment amount" className="field" onChange={(event) => setPayAmount(event.target.value)} placeholder={`Up to ${line.remaining}`} required value={payAmount} />
              <input aria-label="Payment reference" className="field" onChange={(event) => setPayRef(event.target.value)} placeholder="Reference" value={payRef} />
              {pay.error instanceof ApiError ? <p role="alert">{pay.error.message}</p> : null}
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" disabled={pay.isPending} type="submit">Record payment</button>
            </form>
          ) : null}
          {line.locked ? <button className="mt-4 min-h-11 rounded-full border border-line px-4" disabled={slip.isPending} onClick={() => slip.mutate()} type="button">{slip.isPending ? "Loading payslip..." : "View payslip data"}</button> : null}
          {slip.data ? <pre className="mt-3 overflow-auto rounded-xl bg-background p-3 text-xs">{JSON.stringify(slip.data, null, 2)}</pre> : null}
          {line.adjustments.length > 0 ? (
            <ul className="mt-4 grid gap-1 text-sm">
              {line.adjustments.map((item) => <li key={item.id}>{item.kind}: {item.label} {money(item.amount, currency)}</li>)}
            </ul>
          ) : null}
        </section>
      )}
    </aside>
  );
}

export function CompensationPanel({ rows, onSaved }: { rows: Awaited<ReturnType<typeof listCompensation>>; onSaved: () => void }) {
  const [staffId, setStaffId] = useState("");
  const [base, setBase] = useState("");
  const [allowances, setAllowances] = useState("");
  const save = useMutation({
    mutationFn: () => saveCompensation(staffId, Number(base), Number(allowances), new Date().toISOString().slice(0, 10), null),
    onSuccess: onSaved,
  });
  return (
    <div className="mt-6 rounded-xl border border-line p-4">
      <h3 className="font-semibold">Staff salary setup</h3>
      <p className="mt-1 text-sm text-muted">Set effective base salary and allowances before preparing payroll.</p>
      <form className="mt-3 grid gap-2 md:grid-cols-4" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
        <select aria-label="Staff" className="field" onChange={(event) => setStaffId(event.target.value)} required value={staffId}>
          <option value="">Select staff</option>
          {rows.map((row) => <option key={row.staffProfileId} value={row.staffProfileId}>{row.employeeName} · {money(row.baseSalary, row.currency)}</option>)}
        </select>
        <input aria-label="Base salary" className="field" onChange={(event) => setBase(event.target.value)} placeholder="Base salary" required value={base} />
        <input aria-label="Allowances" className="field" onChange={(event) => setAllowances(event.target.value)} placeholder="Allowances" required value={allowances} />
        <button className="min-h-11 rounded-full border border-line px-4" disabled={save.isPending} type="submit">Save salary</button>
      </form>
      {save.error instanceof ApiError ? <p className="mt-2" role="alert">{save.error.message}</p> : null}
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return <div className="flex items-center justify-between gap-3"><dt className="text-muted">{label}</dt><dd className="font-medium">{value}</dd></div>;
}
