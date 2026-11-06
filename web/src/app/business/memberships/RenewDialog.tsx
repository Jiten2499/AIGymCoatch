"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import type { BusinessConfiguration } from "../../../lib/configuration";
import type { GymMembership, MembershipPackage } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { noPtPackagesMessage, ptPackageLabel, type PtPackageOption } from "../../../lib/ptPackage";
import { renewalStart } from "./renewal";

const paymentMethods = ["Cash", "Upi", "DebitCard", "CreditCard", "BankTransfer", "OnlinePayment", "Cheque", "Other"] as const;
const blocked = new Set(["Cancelled", "Suspended", "Pending"]);

type TrainerOption = { id: string; firstName: string; lastName: string; status: string };
type MemberDetails = { assignedTrainerId: string | null; assignedTrainerName: string | null };
type Term = { startDate: string; endDate: string; price: number; currency: string };

export function RenewDialog({ membership, tenantId, onCancel, onRenewed }: { membership: GymMembership; tenantId: string | null; onCancel: () => void; onRenewed: () => void }) {
  const today = localToday();
  const proposed = renewalStart(membership.status, membership.endDate, today);
  const plans = useQuery({ queryKey: ["membership-plans", tenantId, "renew"], enabled: tenantId !== null, queryFn: () => api<Page<MembershipPackage>>("/membership-plans?page=1&pageSize=50") });
  const trainers = useQuery({ queryKey: ["trainers", tenantId, "renew"], enabled: tenantId !== null, queryFn: () => api<Page<TrainerOption>>("/trainers?page=1&pageSize=50") });
  const packages = useQuery({ queryKey: ["pt-packages", tenantId, "renew"], enabled: tenantId !== null, queryFn: () => api<PtPackageOption[]>("/pt-packages?activeOnly=true") });
  const config = useQuery({ queryKey: ["configuration", tenantId, "renew"], enabled: tenantId !== null, queryFn: () => api<BusinessConfiguration>("/configuration") });
  const member = useQuery({ queryKey: ["member", membership.memberProfileId], queryFn: () => api<MemberDetails>(`/members/${membership.memberProfileId}`) });
  const [planId, setPlanId] = useState(membership.membershipPlanId);
  const [startDate, setStartDate] = useState(proposed ?? today);
  const [trainerId, setTrainerId] = useState("");
  const [trainerReady, setTrainerReady] = useState(false);
  const [ptPackageId, setPtPackageId] = useState("");
  const [discountRuleId, setDiscountRuleId] = useState("");
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentMethod, setPaymentMethod] = useState<(typeof paymentMethods)[number]>("Cash");
  const [paymentDate, setPaymentDate] = useState(today);
  const [paymentReference, setPaymentReference] = useState("");
  const [paymentNotes, setPaymentNotes] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const term = useQuery({
    queryKey: ["membership-term", planId, startDate],
    enabled: planId !== "" && startDate !== "",
    queryFn: () => api<Term>(`/membership-plans/${planId}/term?start=${startDate}`),
  });
  useEffect(() => {
    if (!trainerReady && member.data) {
      setTrainerId(member.data.assignedTrainerId ?? "");
      setTrainerReady(true);
    }
  }, [member.data, trainerReady]);

  const selectedPlan = (plans.data?.items ?? []).find((plan) => plan.id === planId);
  const activePlans = (plans.data?.items ?? []).filter((plan) => plan.isActive || plan.id === membership.membershipPlanId);
  const packagePrice = selectedPlan?.price ?? term.data?.price ?? 0;
  const currency = selectedPlan?.currency ?? term.data?.currency ?? membership.currency ?? "";
  const discount = (config.data?.discounts ?? []).find((rule) => rule.id === discountRuleId && rule.isActive);
  const discountAmount = discount
    ? Math.min(packagePrice, discount.discountType === "Percentage" ? (packagePrice * discount.value) / 100 : discount.value)
    : 0;
  const renewalAmount = Math.max(0, packagePrice - discountAmount);
  const pt = (packages.data ?? []).find((item) => item.id === ptPackageId);
  const payable = renewalAmount + (pt?.price ?? 0);
  const earlyBlocked = config.data?.membership.allowEarlyRenewal === false && membership.endDate >= today && membership.status !== "Expired";
  const ineligible = proposed === null || blocked.has(membership.status);
  const renew = useMutation({
    mutationFn: () => api(`/memberships/${membership.id}/renew`, {
      method: "POST",
      body: JSON.stringify({
        reason: null,
        autoRenew: false,
        planId,
        startDate,
        trainerId: trainerId || null,
        clearTrainer: trainerReady && trainerId === "",
        ptPackageId: ptPackageId || null,
        discountRuleId: discountRuleId || null,
        paymentAmount: paymentAmount.trim() ? Number(paymentAmount) : null,
        paymentMethod: paymentAmount.trim() ? paymentMethod : null,
        paymentDate: paymentAmount.trim() ? paymentDate || null : null,
        paymentReferenceNumber: paymentReference.trim() || null,
        paymentNotes: paymentNotes.trim() || null,
      }),
    }),
    onSuccess: () => onRenewed(),
  });

  return (
    <FormDialog large title="Renew membership" onCancel={onCancel}>
      <form className="mt-4 grid gap-4" onSubmit={(event) => {
        event.preventDefault();
        if (!confirmed || ineligible || earlyBlocked || !selectedPlan?.isActive) {
          setMessage(ineligible ? "This membership cannot be renewed." : earlyBlocked ? "Early renewal is turned off for this gym." : "Choose an active package and confirm the renewal.");
          return;
        }
        if (paymentAmount.trim() && !paymentDate) {
          setMessage("Enter a payment date when recording a payment.");
          return;
        }
        setMessage(null);
        renew.mutate();
      }}>
        <section className="grid gap-1 text-sm">
          <h3 className="font-semibold">Current membership</h3>
          <p>{membership.memberName || "Member"} · {membership.planName || "Package"}</p>
          <p>{membership.startDate} to {membership.endDate} · {membership.status}</p>
          <p>Trainer: {member.data?.assignedTrainerName || "None"}</p>
          <p>Outstanding balance: {membership.outstanding ?? 0} {membership.currency ?? ""} · {membership.settlementStatus ?? "Unpaid"}. This is separate from the renewal amount.</p>
        </section>
        <FormField label="Membership package">
          <select className="field" onChange={(event) => setPlanId(event.target.value)} value={planId}>
            {activePlans.map((plan) => <option key={plan.id} value={plan.id}>{plan.name} · {plan.durationCount} {plan.durationUnit} · {plan.price} {plan.currency}</option>)}
          </select>
        </FormField>
        <p className="text-sm text-muted">{selectedPlan ? `${selectedPlan.durationCount} ${selectedPlan.durationUnit} · ${selectedPlan.includedPtSessions > 0 ? `${selectedPlan.includedPtSessions} included PT sessions from this membership` : "No included PT sessions"}` : "Loading package"}{term.data ? ` · ends ${term.data.endDate}` : ""}</p>
        <FormField label="Renewal start date">
          <input className="field" min={proposed ?? undefined} onChange={(event) => setStartDate(event.target.value)} required type="date" value={startDate} />
        </FormField>
        <FormField label="Trainer">
          <select className="field" onChange={(event) => setTrainerId(event.target.value)} value={trainerId}>
            <option value="">No trainer</option>
            {(trainers.data?.items ?? []).filter((trainer) => trainer.status === "Active" || trainer.id === trainerId).map((trainer) => <option key={trainer.id} value={trainer.id}>{trainer.firstName} {trainer.lastName}</option>)}
          </select>
        </FormField>
        <FormField label="Standalone PT package (optional)">
          <select className="field" onChange={(event) => setPtPackageId(event.target.value)} value={ptPackageId}>
            <option value="">None</option>
            {(packages.data ?? []).filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{ptPackageLabel(item)}</option>)}
          </select>
        </FormField>
        {(packages.data ?? []).length === 0 ? <p className="text-sm text-muted">{noPtPackagesMessage}</p> : null}
        <p className="text-sm text-muted">Included sessions: {selectedPlan?.includedPtSessions ?? 0}. Standalone sessions: {pt?.sessionCount ?? 0}. Unused sessions from the current membership are not carried over. A standalone package stays unpaid until a payment is recorded.</p>
        {(selectedPlan?.includedPtSessions ?? 0) > 0 && (pt?.sessionCount ?? 0) > 0 ? <p className="text-sm">Combined usable sessions: {(selectedPlan?.includedPtSessions ?? 0) + (pt?.sessionCount ?? 0)}</p> : null}
        <FormField label="Discount (optional)">
          <select className="field" onChange={(event) => setDiscountRuleId(event.target.value)} value={discountRuleId}>
            <option value="">None</option>
            {(config.data?.discounts ?? []).filter((rule) => rule.isActive).map((rule) => <option key={rule.id} value={rule.id}>{rule.name}</option>)}
          </select>
        </FormField>
        <section className="grid gap-1 text-sm">
          <h3 className="font-semibold">Renewal summary</h3>
          <p>Package {packagePrice} {currency} · Discount {discountAmount} · Membership {renewalAmount} {currency}</p>
          <p>Standalone PT {pt?.price ?? 0} {pt?.currency ?? currency} · Payable now {payable} {currency}</p>
          <p>Payment is optional. Without one, the renewal stays unpaid. A payment records only against the membership amount.</p>
        </section>
        <FormField label="Amount paid toward the membership (optional)"><input className="field" min={0} onChange={(event) => setPaymentAmount(event.target.value)} step="0.01" type="number" value={paymentAmount} /></FormField>
        <FormField label="Payment method"><select className="field" onChange={(event) => setPaymentMethod(event.target.value as (typeof paymentMethods)[number])} value={paymentMethod}>{paymentMethods.map((method) => <option key={method}>{method}</option>)}</select></FormField>
        <FormField label="Payment date"><input className="field" onChange={(event) => setPaymentDate(event.target.value)} type="date" value={paymentDate} /></FormField>
        <FormField label="Reference number"><input className="field" onChange={(event) => setPaymentReference(event.target.value)} value={paymentReference} /></FormField>
        <label className="flex items-center gap-2 text-sm"><input checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} type="checkbox" />I confirm this renewal.</label>
        {message ? <p role="alert">{message}</p> : null}
        {renew.error instanceof ApiError ? <p role="alert">{renew.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={renew.isPending} submitLabel="Renew membership" />
      </form>
    </FormDialog>
  );
}

function localToday(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
}
