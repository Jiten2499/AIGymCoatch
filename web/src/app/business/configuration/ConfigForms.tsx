"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { ScrollableGrid } from "../../../components/ScrollableGrid";
import { api, ApiError } from "../../../lib/api";
import type { AttendanceConfig, CrmCatalogItem, CrmConfig, DiscountRule, FreezeConfig, MemberCategory, MembershipConfig } from "../../../lib/configuration";
import type { MembershipPackage } from "../../../lib/gym";
import { emptyToNull } from "../../../lib/gym";

function useSyncedDraft<T>(value: T) {
  const [draft, setDraft] = useState(value);
  const [source, setSource] = useState(value);
  if (value !== source) {
    setSource(value);
    setDraft(value);
  }
  return [draft, setDraft] as const;
}

export function MembershipForm({ value, tenantId }: { value: MembershipConfig; tenantId: string | null }) {
  const client = useQueryClient();
  const [draft, setDraft] = useSyncedDraft(value);
  const save = useMutation({
    mutationFn: () => api("/configuration/membership", { method: "PUT", body: JSON.stringify(draft) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); },
  });
  return (
    <form className="mt-4 grid max-w-xl gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
      <Toggle checked={draft.allowOverlappingMemberships} label="Allow overlapping active memberships" onChange={(allowOverlappingMemberships) => setDraft({ ...draft, allowOverlappingMemberships })} />
      <Toggle checked={draft.allowEarlyRenewal} label="Allow early renewal" onChange={(allowEarlyRenewal) => setDraft({ ...draft, allowEarlyRenewal })} />
      <Toggle checked={draft.defaultStartOnJoinDate} label="Default membership start to joining date" onChange={(defaultStartOnJoinDate) => setDraft({ ...draft, defaultStartOnJoinDate })} />
      <Toggle checked={draft.autoExpireEnabled} label="Auto-expire memberships after end date" onChange={(autoExpireEnabled) => setDraft({ ...draft, autoExpireEnabled })} />
      <FormField label="Grace period after expiry (days)"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, gracePeriodDays: Number(event.target.value) })} type="number" value={draft.gracePeriodDays} /></FormField>
      {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
      <FormActions cancel={() => setDraft(value)} pending={save.isPending} submitLabel="Save membership" />
    </form>
  );
}

export function FreezeForm({ value, tenantId }: { value: FreezeConfig; tenantId: string | null }) {
  const client = useQueryClient();
  const [draft, setDraft] = useSyncedDraft(value);
  const save = useMutation({
    mutationFn: () => api("/configuration/freeze", { method: "PUT", body: JSON.stringify(draft) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); },
  });
  return (
    <form className="mt-4 grid max-w-xl gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
      <FormField label="Maximum freeze days"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, maxFreezeDays: Number(event.target.value) })} type="number" value={draft.maxFreezeDays} /></FormField>
      <FormField label="Minimum freeze days"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, minFreezeDays: Number(event.target.value) })} type="number" value={draft.minFreezeDays} /></FormField>
      <FormField label="Maximum freeze occurrences"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, maxFreezeOccurrences: Number(event.target.value) })} type="number" value={draft.maxFreezeOccurrences} /></FormField>
      <Toggle checked={draft.freezeRequiresApproval} label="Freeze requires approval" onChange={(freezeRequiresApproval) => setDraft({ ...draft, freezeRequiresApproval })} />
      <FormField label="Maximum extension days"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, maxExtensionDays: Number(event.target.value) })} type="number" value={draft.maxExtensionDays} /></FormField>
      <Toggle checked={draft.extensionRequiresApproval} label="Extension requires approval" onChange={(extensionRequiresApproval) => setDraft({ ...draft, extensionRequiresApproval })} />
      {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
      <FormActions cancel={() => setDraft(value)} pending={save.isPending} submitLabel="Save freeze rules" />
    </form>
  );
}

export function AttendanceForm({ value, tenantId }: { value: AttendanceConfig; tenantId: string | null }) {
  const client = useQueryClient();
  const [draft, setDraft] = useSyncedDraft(value);
  const save = useMutation({
    mutationFn: () => api("/configuration/attendance", { method: "PUT", body: JSON.stringify({ ...draft, openingTime: emptyToNull(draft.openingTime ?? ""), closingTime: emptyToNull(draft.closingTime ?? "") }) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); },
  });
  return (
    <form className="mt-4 grid max-w-xl gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
      <FormField label="Duplicate check-in interval (minutes)"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, duplicateCheckInMinutes: Number(event.target.value) })} type="number" value={draft.duplicateCheckInMinutes} /></FormField>
      <FormField label="Opening time"><input className="field" onChange={(event) => setDraft({ ...draft, openingTime: event.target.value })} type="time" value={draft.openingTime ?? ""} /></FormField>
      <FormField label="Closing time"><input className="field" onChange={(event) => setDraft({ ...draft, closingTime: event.target.value })} type="time" value={draft.closingTime ?? ""} /></FormField>
      <FormField label="Check-in grace period (minutes)"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, checkInGraceMinutes: Number(event.target.value) })} type="number" value={draft.checkInGraceMinutes} /></FormField>
      <FormField label="Default attendance method">
        <select className="field" onChange={(event) => setDraft({ ...draft, defaultCheckInMethod: event.target.value })} value={draft.defaultCheckInMethod}>
          {["Manual", "QR", "Reception", "Other"].map((method) => <option key={method} value={method}>{method}</option>)}
        </select>
      </FormField>
      {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
      <FormActions cancel={() => setDraft(value)} pending={save.isPending} submitLabel="Save attendance" />
    </form>
  );
}

export function CrmDefaultsForm({ value, tenantId }: { value: CrmConfig; tenantId: string | null }) {
  const client = useQueryClient();
  const [draft, setDraft] = useSyncedDraft(value);
  const save = useMutation({
    mutationFn: () => api("/configuration/crm", { method: "PUT", body: JSON.stringify(draft) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); },
  });
  return (
    <form className="mt-4 grid max-w-xl gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
      <FormField label="Default trial days"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, defaultTrialDays: Number(event.target.value) })} type="number" value={draft.defaultTrialDays} /></FormField>
      <FormField label="Follow-up reminder hours"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, followUpReminderHours: Number(event.target.value) })} type="number" value={draft.followUpReminderHours} /></FormField>
      <FormField label="Default follow-up days"><input className="field" min={0} onChange={(event) => setDraft({ ...draft, defaultFollowUpDays: Number(event.target.value) })} type="number" value={draft.defaultFollowUpDays} /></FormField>
      {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
      <FormActions cancel={() => setDraft(value)} pending={save.isPending} submitLabel="Save CRM defaults" />
    </form>
  );
}

export function CategoryDialog({ initial, onCancel, onSaved }: { initial?: MemberCategory; onCancel: () => void; onSaved: () => void }) {
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const save = useMutation({
    mutationFn: () => api(initial ? `/configuration/member-categories/${initial.id}` : "/configuration/member-categories", { method: initial ? "PUT" : "POST", body: JSON.stringify({ name, description: emptyToNull(description) }) }),
    onSuccess: onSaved,
  });
  return (
    <FormDialog title={initial ? "Edit category" : "Add category"} onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
        <FormField label="Name"><input className="field" onChange={(event) => setName(event.target.value)} required value={name} /></FormField>
        <FormField label="Description"><input className="field" onChange={(event) => setDescription(event.target.value)} value={description} /></FormField>
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={save.isPending} submitLabel="Save" />
      </form>
    </FormDialog>
  );
}

export function DiscountDialog({ initial, packages, categories, onCancel, onSaved }: { initial?: DiscountRule; packages: MembershipPackage[]; categories: MemberCategory[]; onCancel: () => void; onSaved: () => void }) {
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [discountType, setDiscountType] = useState(initial?.discountType ?? "Percentage");
  const [value, setValue] = useState(String(initial?.value ?? "10"));
  const [appliesTo, setAppliesTo] = useState(initial?.appliesTo ?? "AllPackages");
  const [packageId, setPackageId] = useState(initial?.packageId ?? "");
  const [packageCategory, setPackageCategory] = useState(initial?.packageCategory ?? "Standard");
  const [memberCategoryId, setMemberCategoryId] = useState(initial?.memberCategoryId ?? "");
  const [startDate, setStartDate] = useState(initial?.startDate ?? "");
  const [endDate, setEndDate] = useState(initial?.endDate ?? "");
  const save = useMutation({
    mutationFn: () => api(initial ? `/configuration/discounts/${initial.id}` : "/configuration/discounts", {
      method: initial ? "PUT" : "POST",
      body: JSON.stringify({
        name,
        description: emptyToNull(description),
        discountType,
        value: Number(value),
        appliesTo,
        packageId: appliesTo === "SpecificPackage" ? packageId || null : null,
        packageCategory: appliesTo === "PackageCategory" ? packageCategory : null,
        memberCategoryId: appliesTo === "MemberCategory" ? memberCategoryId || null : null,
        minimumPackagePrice: null,
        minimumDurationDays: null,
        maximumDiscountAmount: null,
        startDate: emptyToNull(startDate),
        endDate: emptyToNull(endDate),
      }),
    }),
    onSuccess: onSaved,
  });
  return (
    <FormDialog title={initial ? "Edit discount" : "Add discount"} onCancel={onCancel} wide>
      <form className="mt-4 grid gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
        <FormField label="Name"><input className="field" onChange={(event) => setName(event.target.value)} required value={name} /></FormField>
        <FormField label="Description"><input className="field" onChange={(event) => setDescription(event.target.value)} value={description} /></FormField>
        <FormField label="Type"><select className="field" onChange={(event) => setDiscountType(event.target.value)} value={discountType}><option value="Percentage">Percentage</option><option value="FixedAmount">Fixed amount</option></select></FormField>
        <FormField label="Value"><input className="field" min={0} onChange={(event) => setValue(event.target.value)} required step="0.01" type="number" value={value} /></FormField>
        <FormField label="Applies to">
          <select className="field" onChange={(event) => setAppliesTo(event.target.value)} value={appliesTo}>
            <option value="AllPackages">All packages</option>
            <option value="SpecificPackage">Specific package</option>
            <option value="PackageCategory">Package category</option>
            <option value="MemberCategory">Member category</option>
          </select>
        </FormField>
        {appliesTo === "SpecificPackage" ? <FormField label="Package"><select className="field" onChange={(event) => setPackageId(event.target.value)} required value={packageId}><option value="">Select</option>{packages.map((plan) => <option key={plan.id} value={plan.id}>{plan.name}</option>)}</select></FormField> : null}
        {appliesTo === "PackageCategory" ? <FormField label="Package category"><select className="field" onChange={(event) => setPackageCategory(event.target.value)} value={packageCategory}>{["Standard", "Student", "Women", "Corporate", "Couple"].map((item) => <option key={item} value={item}>{item}</option>)}</select></FormField> : null}
        {appliesTo === "MemberCategory" ? <FormField label="Member category"><select className="field" onChange={(event) => setMemberCategoryId(event.target.value)} required value={memberCategoryId}><option value="">Select</option>{categories.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></FormField> : null}
        <FormField label="Valid from"><input className="field" onChange={(event) => setStartDate(event.target.value)} type="date" value={startDate} /></FormField>
        <FormField label="Valid until"><input className="field" onChange={(event) => setEndDate(event.target.value)} type="date" value={endDate} /></FormField>
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={save.isPending} submitLabel="Save" />
      </form>
    </FormDialog>
  );
}

export function CatalogAddDialog({ title, path, onCancel, onSaved }: { title: string; path: string; onCancel: () => void; onSaved: () => void }) {
  const [name, setName] = useState("");
  const save = useMutation({
    mutationFn: () => api(path, { method: "POST", body: JSON.stringify({ name }) }),
    onSuccess: onSaved,
  });
  return (
    <FormDialog title={title} onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
        <FormField label="Name"><input className="field" onChange={(event) => setName(event.target.value)} required value={name} /></FormField>
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={save.isPending} submitLabel="Save" />
      </form>
    </FormDialog>
  );
}

export function CatalogList({ items, path, tenantId, onAdd }: { items: CrmCatalogItem[]; path: string; tenantId: string | null; onAdd: () => void }) {
  const client = useQueryClient();
  const status = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => api(`${path}/${id}/status`, { method: "POST", body: JSON.stringify({ active }) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); },
  });
  return (
    <div className="mt-4">
      <button className="min-h-11 rounded-full border border-line px-4" onClick={onAdd} type="button">Add</button>
      <ScrollableGrid className="mt-3">
      <ul className="grid w-max min-w-full gap-2">
        {items.map((item) => (
          <li className="flex items-center justify-between gap-3 rounded-xl border border-line px-4 py-3" key={item.id}>
            <span>{item.name}{item.isSystem ? " · system" : ""} · {item.isActive ? "Active" : "Inactive"}</span>
            <button className="min-h-11 rounded-full border border-line px-4" onClick={() => status.mutate({ id: item.id, active: !item.isActive })} type="button">{item.isActive ? "Deactivate" : "Activate"}</button>
          </li>
        ))}
      </ul>
      </ScrollableGrid>
    </div>
  );
}

function Toggle({ checked, label, onChange }: { checked: boolean; label: string; onChange: (value: boolean) => void }) {
  return (
    <label className="flex min-h-11 items-center gap-3">
      <input checked={checked} onChange={(event) => onChange(event.target.checked)} type="checkbox" />
      <span>{label}</span>
    </label>
  );
}
