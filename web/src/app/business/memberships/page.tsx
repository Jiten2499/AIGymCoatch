"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { AppAccessFields, emptyAppAccess, type AppAccessDraft } from "../../../components/AppAccessFields";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import type { BusinessConfiguration } from "../../../lib/configuration";
import type { BranchOption, GymMember, GymMembership, MembershipPackage } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { getSession, subscribeSession } from "../../../lib/session";
import { FreezeDialog } from "./FreezeDialog";
import { RenewDialog } from "./RenewDialog";
import { PackageDialog } from "./PackageDialog";

export default function MembershipsPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const [adding, setAdding] = useState(false);
  const [assigning, setAssigning] = useState(false);
  const [freezing, setFreezing] = useState(false);
  const [renewing, setRenewing] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const packages = useQuery({
    queryKey: ["membership-plans", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<MembershipPackage>>("/membership-plans?page=1&pageSize=50"),
  });
  const memberships = useQuery({
    queryKey: ["memberships", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<GymMembership>>("/memberships?page=1&pageSize=50&sort=startDate"),
  });
  const change = useMutation({
    mutationFn: (action: "unfreeze" | "cancel") => api(`/memberships/${selected}/${action}`, { method: "POST", body: JSON.stringify({ reason: null, autoRenew: false, days: 0 }) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["memberships", tenantId] }); },
  });
  const error = packages.error ?? memberships.error;
  const rows = memberships.data?.items ?? [];
  const selectedMembership = rows.find((row) => row.id === selected) ?? null;

  return (
    <PageState error={error instanceof ApiError ? error.message : null} loading={packages.isLoading || memberships.isLoading} onRetry={() => { void packages.refetch(); void memberships.refetch(); }}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Memberships</h1>
          <p className="mt-2 max-w-3xl text-muted">Packages this gym sells, and the memberships assigned to members.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAdding(true)} type="button">Add package</button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setAssigning(true)} type="button">Assign membership</button>
        </div>
      </div>
      <h2 className="mt-8 text-xl font-semibold">Packages</h2>
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Name", cell: (row) => row.name },
            { header: "Type", cell: (row) => row.membershipType },
            { header: "Price", cell: (row) => `${row.price} ${row.currency}` },
            { header: "Duration", cell: (row) => `${row.durationCount} ${row.durationUnit}` },
            { header: "Included PT", cell: (row) => row.includedPtSessions > 0 ? String(row.includedPtSessions) : "None" },
            { header: "Status", cell: (row) => row.isActive ? "Active" : "Inactive" },
          ]}
          empty="No packages were returned."
          rowKey={(row) => row.id}
          rows={packages.data?.items ?? []}
        />
      </div>
      <h2 className="mt-8 text-xl font-semibold">Assigned memberships</h2>
      {change.error instanceof ApiError ? <p className="mt-3" role="alert">{change.error.message}</p> : null}
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Member", cell: (row) => row.memberName || "—" },
            { header: "Package", cell: (row) => row.planName || "—" },
            { header: "Number", cell: (row) => row.membershipNumber },
            { header: "Start", cell: (row) => row.startDate },
            { header: "End", cell: (row) => row.endDate },
            { header: "Status", cell: (row) => row.status },
            { header: "Payment", cell: (row) => `${row.totalPaid ?? 0}/${row.finalAmount ?? 0} ${row.currency ?? ""} · ${row.settlementStatus ?? "—"}` },
          ]}
          empty="No memberships were returned."
          rowKey={(row) => row.id}
          rows={rows}
          selection={{ isSelected: (row) => selected === row.id, label: (row) => `Select ${row.membershipNumber}`, onToggle: (row) => setSelected((current) => (current === row.id ? null : row.id)) }}
        />
      </div>
      {selected ? (
        <div className="mt-4 flex flex-wrap gap-2">
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setRenewing(true)} type="button">Renew</button>
          {selectedMembership?.status === "Active" ? <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setFreezing(true)} type="button">Freeze</button> : null}
          {selectedMembership?.status === "Frozen" ? <button className="min-h-11 rounded-full border border-line px-4" onClick={() => change.mutate("unfreeze")} type="button">Unfreeze</button> : null}
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => change.mutate("cancel")} type="button">Cancel</button>
        </div>
      ) : null}
      {adding ? <PackageDialog onCancel={() => setAdding(false)} onCreated={async () => { setAdding(false); await client.invalidateQueries({ queryKey: ["membership-plans", tenantId] }); }} /> : null}
      {assigning ? <AssignDialog onCancel={() => setAssigning(false)} onCreated={async () => { setAssigning(false); await client.invalidateQueries({ queryKey: ["memberships", tenantId] }); }} tenantId={tenantId} /> : null}
      {freezing && selectedMembership ? <FreezeDialog membership={selectedMembership} onCancel={() => setFreezing(false)} onFrozen={async () => { setFreezing(false); await client.invalidateQueries({ queryKey: ["memberships", tenantId] }); }} tenantId={tenantId} /> : null}
      {renewing && selectedMembership ? <RenewDialog membership={selectedMembership} onCancel={() => setRenewing(false)} onRenewed={async () => { setRenewing(false); await client.invalidateQueries({ queryKey: ["memberships", tenantId] }); }} tenantId={tenantId} /> : null}
    </PageState>
  );
}

const paymentMethods = ["Cash", "Upi", "DebitCard", "CreditCard", "BankTransfer", "OnlinePayment", "Cheque", "Other"] as const;

function AssignDialog({ tenantId, onCancel, onCreated }: { tenantId: string | null; onCancel: () => void; onCreated: () => void }) {
  const members = useQuery({ queryKey: ["members", tenantId, "assign"], enabled: tenantId !== null, queryFn: () => api<Page<GymMember>>("/members?page=1&pageSize=50&sort=name") });
  const plans = useQuery({ queryKey: ["membership-plans", tenantId, "assign"], enabled: tenantId !== null, queryFn: () => api<Page<MembershipPackage>>("/membership-plans?activeOnly=true&page=1&pageSize=50") });
  const branches = useQuery({ queryKey: ["branches", tenantId], enabled: tenantId !== null, queryFn: () => api<Page<BranchOption>>("/branches?page=1&pageSize=50") });
  const config = useQuery({ queryKey: ["configuration", tenantId, "assign"], enabled: tenantId !== null, queryFn: () => api<BusinessConfiguration>("/configuration") });
  const [memberId, setMemberId] = useState("");
  const [planId, setPlanId] = useState("");
  const [branchId, setBranchId] = useState("");
  const [startDate, setStartDate] = useState("");
  const [discountRuleId, setDiscountRuleId] = useState("");
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentMethod, setPaymentMethod] = useState<(typeof paymentMethods)[number]>("Cash");
  const [paymentDate, setPaymentDate] = useState("");
  const [paymentReference, setPaymentReference] = useState("");
  const [paymentNotes, setPaymentNotes] = useState("");
  const [appAccess, setAppAccess] = useState<AppAccessDraft>(emptyAppAccess);
  const [message, setMessage] = useState<string | null>(null);
  const selectedPlan = (plans.data?.items ?? []).find((plan) => plan.id === planId);
  const packagePrice = selectedPlan?.price ?? 0;
  const selectedDiscount = (config.data?.discounts ?? []).find((rule) => rule.id === discountRuleId && rule.isActive);
  const discountAmount = selectedDiscount
    ? Math.min(packagePrice, selectedDiscount.discountType === "Percentage" ? (packagePrice * selectedDiscount.value) / 100 : selectedDiscount.value)
    : 0;
  const finalAmount = Math.max(0, packagePrice - discountAmount);
  const paid = paymentAmount.trim() ? Number(paymentAmount) : 0;
  const outstanding = Math.max(0, finalAmount - (Number.isFinite(paid) ? paid : 0));
  const assign = useMutation({
    mutationFn: () => api("/memberships", {
      method: "POST",
      body: JSON.stringify({
        memberProfileId: memberId,
        branchId: branchId || null,
        planId,
        startDate,
        autoRenew: false,
        notes: null,
        enableBasicAppAccess: false,
        assignAppAccess: appAccess.assignAppAccess,
        appSubscriptionId: appAccess.assignAppAccess ? appAccess.appSubscriptionId || null : null,
        discountRuleId: discountRuleId || null,
        paymentAmount: paymentAmount.trim() ? Number(paymentAmount) : null,
        paymentMethod: paymentAmount.trim() ? paymentMethod : null,
        paymentDate: paymentAmount.trim() ? paymentDate || null : null,
        paymentReferenceNumber: paymentReference.trim() || null,
        paymentNotes: paymentNotes.trim() || null,
      }),
    }),
    onSuccess: () => onCreated(),
  });

  return (
    <FormDialog title="Assign membership" onCancel={onCancel} wide>
      <form
        className="mt-4 grid gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          if (appAccess.assignAppAccess && !appAccess.appSubscriptionId) {
            setMessage("Select a LifeFit app plan.");
            return;
          }
          if (paymentAmount.trim() && !paymentDate) {
            setMessage("Enter a payment date when recording a payment.");
            return;
          }
          setMessage(null);
          assign.mutate();
        }}
      >
        <FormField label="Member"><select className="field" onChange={(event) => setMemberId(event.target.value)} required value={memberId}><option value="">Select</option>{(members.data?.items ?? []).map((member) => <option key={member.id} value={member.id}>{member.fullName}</option>)}</select></FormField>
        <FormField label="Package"><select className="field" onChange={(event) => setPlanId(event.target.value)} required value={planId}><option value="">Select</option>{(plans.data?.items ?? []).map((plan) => <option key={plan.id} value={plan.id}>{plan.name} · {plan.price} {plan.currency}</option>)}</select></FormField>
        <FormField label="Branch"><select className="field" onChange={(event) => setBranchId(event.target.value)} value={branchId}><option value="">None</option>{(branches.data?.items ?? []).map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}</select></FormField>
        <FormField label="Start date"><input className="field" onChange={(event) => setStartDate(event.target.value)} required type="date" value={startDate} /></FormField>
        <FormField label="Discount (optional)">
          <select className="field" onChange={(event) => setDiscountRuleId(event.target.value)} value={discountRuleId}>
            <option value="">None</option>
            {(config.data?.discounts ?? []).filter((rule) => rule.isActive).map((rule) => (
              <option key={rule.id} value={rule.id}>{rule.name} · {rule.discountType === "Percentage" ? `${rule.value}%` : rule.value}</option>
            ))}
          </select>
        </FormField>
        <p className="text-sm text-muted">Package {packagePrice} · Discount {discountAmount} · Final {finalAmount} · Paid {Number.isFinite(paid) ? paid : 0} · Pending {outstanding}</p>
        <FormField label="Amount paid (optional)"><input className="field" min={0} onChange={(event) => setPaymentAmount(event.target.value)} step="0.01" type="number" value={paymentAmount} /></FormField>
        <FormField label="Payment method">
          <select className="field" onChange={(event) => setPaymentMethod(event.target.value as (typeof paymentMethods)[number])} value={paymentMethod}>
            {paymentMethods.map((method) => <option key={method}>{method}</option>)}
          </select>
        </FormField>
        <FormField label="Payment date"><input className="field" onChange={(event) => setPaymentDate(event.target.value)} type="date" value={paymentDate} /></FormField>
        <FormField label="Reference number"><input className="field" onChange={(event) => setPaymentReference(event.target.value)} value={paymentReference} /></FormField>
        <FormField label="Payment notes"><textarea className="field min-h-20 py-2" onChange={(event) => setPaymentNotes(event.target.value)} value={paymentNotes} /></FormField>
        <AppAccessFields draft={appAccess} onChange={(patch) => setAppAccess((current) => ({ ...current, ...patch }))} tenantId={tenantId} />
        {message ? <p role="alert">{message}</p> : null}
        {assign.error instanceof ApiError ? <p role="alert">{assign.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={assign.isPending} submitLabel="Assign" />
      </form>
    </FormDialog>
  );
}
