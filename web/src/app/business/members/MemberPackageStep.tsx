"use client";

import { useState } from "react";
import { memberPaymentMethods, availablePtSessions, type MemberDraft } from "./memberWizard";
import type { BranchOption, MembershipPackage } from "../../../lib/gym";
import { noPtPackagesMessage, ptPackageLabel, type PtPackageOption } from "../../../lib/ptPackage";

export type MembershipTerm = { startDate: string; endDate: string; status: string; price: number; currency: string };
export type { PtPackageOption };

export function MembershipPackageStep({
  branches,
  draft,
  plans,
  ptPackages,
  term,
  update,
}: {
  branches: BranchOption[];
  draft: MemberDraft;
  plans: MembershipPackage[];
  ptPackages: PtPackageOption[];
  term?: MembershipTerm;
  update: (patch: Partial<MemberDraft>) => void;
}) {
  const [query, setQuery] = useState("");
  const selected = plans.find((plan) => plan.id === draft.planId);
  const selectedPt = ptPackages.find((item) => item.id === draft.ptPackageId);
  const visible = plans.filter((plan) => plan.isActive && plan.name.toLowerCase().includes(query.trim().toLowerCase()));
  const included = selected?.includedPtSessions ?? 0;
  return (
    <div className="grid gap-4">
      <section className="rounded-2xl border border-line bg-background p-4">
        <h3 className="text-base font-semibold">Gym membership</h3>
        <p className="mt-1 text-sm leading-6 text-muted">Choose an active package, or leave this empty. A member can be saved without a membership.</p>
        <label className="mt-3 grid gap-2 text-sm">Search packages
          <input aria-label="Search packages" className="field" onChange={(event) => setQuery(event.target.value)} placeholder="Search active packages" value={query} />
        </label>
        <label className="mt-3 grid gap-2 text-sm">Membership package
          <select aria-label="Membership package" className="field" onChange={(event) => update({ planId: event.target.value })} value={draft.planId}>
            <option value="">No membership</option>
            {visible.map((plan) => <option key={plan.id} value={plan.id}>{plan.name} · {plan.price} {plan.currency}</option>)}
          </select>
        </label>
        {selected ? (
          <div className="mt-3 grid gap-2 text-sm">
            <p>{selected.name} · {selected.durationCount} {selected.durationUnit} · {selected.price} {selected.currency}</p>
            <p className="text-muted">{selected.description || "No additional benefits listed."}</p>
            <p>{included > 0 ? `Includes ${included} personal-training sessions with this membership.` : "No membership-included personal-training sessions."}</p>
            <label className="grid gap-2">Branch
              <select className="field" onChange={(event) => update({ branchId: event.target.value })} value={draft.branchId}><option value="">None</option>{branches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}</select>
            </label>
            <label className="grid gap-2">Membership start date
              <input className="field" onChange={(event) => update({ startDate: event.target.value })} type="date" value={draft.startDate} />
            </label>
            <p>Ends {term?.endDate ?? "Calculating..."} · {term?.status ?? ""}</p>
            <label className="grid gap-2">Amount paid (optional)
              <input className="field" min={0} onChange={(event) => update({ paymentAmount: event.target.value })} step="0.01" type="number" value={draft.paymentAmount} />
            </label>
            <label className="grid gap-2">Payment method
              <select className="field" onChange={(event) => update({ paymentMethod: event.target.value })} value={draft.paymentMethod}>
                {memberPaymentMethods.map((method) => <option key={method}>{method}</option>)}
              </select>
            </label>
            <label className="grid gap-2">Payment date
              <input className="field" onChange={(event) => update({ paymentDate: event.target.value })} type="date" value={draft.paymentDate} />
            </label>
            <label className="grid gap-2">Reference number
              <input className="field" onChange={(event) => update({ paymentReference: event.target.value })} value={draft.paymentReference} />
            </label>
            <label className="grid gap-2">Payment notes
              <textarea className="field min-h-20 py-2" onChange={(event) => update({ paymentNotes: event.target.value })} value={draft.paymentNotes} />
            </label>
          </div>
        ) : <p className="mt-3 text-sm text-muted">No membership selected. The member can still be saved.</p>}
      </section>
      <section className="rounded-2xl border border-line bg-background p-4">
        <h3 className="text-base font-semibold">Personal training package</h3>
        <p className="mt-1 text-sm leading-6 text-muted">Optional. This is a separate package from sessions included in a gym membership. Assigning it does not mark it paid.</p>
        <label className="mt-3 grid gap-2 text-sm">PT package
          <select aria-label="PT package" className="field" onChange={(event) => update({ ptPackageId: event.target.value })} value={draft.ptPackageId}>
            <option value="">No PT package</option>
            {ptPackages.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{ptPackageLabel(item)}</option>)}
          </select>
        </label>
        {ptPackages.length === 0 ? <p className="mt-3 text-sm text-muted">{noPtPackagesMessage}</p> : null}
        {selectedPt ? (
          <p className="mt-3 text-sm">{selectedPt.name} · {selectedPt.sessionCount} sessions · valid {selectedPt.validityDays} days · {selectedPt.price} {selectedPt.currency} · Unpaid</p>
        ) : <p className="mt-3 text-sm text-muted">No standalone personal-training package selected.</p>}
      </section>
    </div>
  );
}

export function MemberReview({
  draft,
  plan,
  ptPackage,
  term,
  trainerName,
}: {
  draft: MemberDraft;
  plan: MembershipPackage | null;
  ptPackage: PtPackageOption | null;
  term: MembershipTerm | null;
  trainerName: string;
}) {
  const sessions = availablePtSessions(plan?.includedPtSessions ?? 0, ptPackage?.sessionCount ?? null);
  return (
    <div className="grid gap-2 text-sm">
      <p>{draft.fullName}</p>
      <p>{draft.phone}{draft.email ? ` · ${draft.email}` : ""}</p>
      <p>Joined {draft.joinDate} · {draft.status}</p>
      <p>Assigned trainer: {trainerName || "None"}</p>
      {plan && term ? <p>{plan.name} · {term.startDate} to {term.endDate} · {term.status} · {term.price} {term.currency}</p> : <p>No membership selected.</p>}
      <p>{sessions.included > 0 ? `Membership-included PT sessions: ${sessions.included}` : "No membership-included personal-training sessions."}</p>
      {ptPackage ? <p>Standalone PT package: {ptPackage.name} · {ptPackage.sessionCount} sessions · {ptPackage.validityDays} days · {ptPackage.price} {ptPackage.currency} · Unpaid</p> : <p>No standalone personal-training package.</p>}
      <p>Total available PT sessions: {sessions.total}</p>
      {draft.planId && draft.paymentAmount.trim() ? <p>Paid {draft.paymentAmount} by {draft.paymentMethod} on {draft.paymentDate}</p> : null}
      <p>Basic LifeFit login: {draft.enableBasicAppAccess || draft.assignAppAccess || draft.email ? "Yes" : "No"}</p>
      <p>LifeFit app plan seat: {draft.assignAppAccess ? "Yes" : "No"}</p>
    </div>
  );
}
