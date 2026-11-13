"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import { configTabs, type BusinessConfiguration, type ConfigTab, type DiscountRule, type MemberCategory } from "../../../lib/configuration";
import type { MembershipPackage } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { getSession, subscribeSession } from "../../../lib/session";
import { AttendanceForm, CatalogAddDialog, CatalogList, CategoryDialog, CrmDefaultsForm, DiscountDialog, FreezeForm, MembershipForm } from "./ConfigForms";
import { CoachRewardsSection } from "./CoachRewardsSection";
import { ChatPermissionsSection } from "./ChatPermissionsSection";
import { PtPackagesSection } from "./PtPackagesSection";
import { RolesPermissionsSection } from "./RolesPermissionsSection";

export default function ConfigurationPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const [tab, setTab] = useState<ConfigTab>("Membership");
  const features = useQuery({
    queryKey: ["features", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<{ featureCode: string; enabled: boolean }[]>("/features"),
  });
  const chatEnabled = features.data?.some((feature) => feature.featureCode === "GYM_CHAT" && feature.enabled) === true;
  const tabs = configTabs.filter((item) => item !== "Chat" || chatEnabled);
  const [categoryDialog, setCategoryDialog] = useState<MemberCategory | true | null>(null);
  const [discountDialog, setDiscountDialog] = useState<DiscountRule | true | null>(null);
  const [sourceDialog, setSourceDialog] = useState(false);
  const [reasonDialog, setReasonDialog] = useState(false);
  const configuration = useQuery({
    queryKey: ["configuration", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<BusinessConfiguration>("/configuration"),
  });
  const packages = useQuery({
    queryKey: ["membership-plans", tenantId, "configuration"],
    enabled: tenantId !== null && (tab === "Discounts" || discountDialog !== null),
    queryFn: () => api<Page<MembershipPackage>>("/membership-plans?activeOnly=true&page=1&pageSize=50"),
  });
  const categoryStatus = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => api(`/configuration/member-categories/${id}/status`, { method: "POST", body: JSON.stringify({ active }) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); },
  });
  const discountStatus = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => api(`/configuration/discounts/${id}/status`, { method: "POST", body: JSON.stringify({ active }) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); },
  });

  const data = configuration.data;

  return (
    <PageState error={configuration.error instanceof ApiError ? configuration.error.message : null} loading={configuration.isLoading} onRetry={() => void configuration.refetch()}>
      <div>
        <h1 className="text-3xl font-semibold">Configuration</h1>
        <p className="mt-2 max-w-3xl text-muted">Operational rules for memberships, discounts, member categories, personal-training packages, coach recognition rewards, freezes, attendance, and CRM for this gym.</p>
      </div>
      <div className="mt-6 flex flex-wrap gap-2" role="tablist" aria-label="Configuration sections">
        {tabs.map((item) => (
          <button
            aria-selected={tab === item}
            className={`min-h-11 rounded-full px-4 ${tab === item ? "bg-accent font-semibold text-background" : "border border-line"}`}
            key={item}
            onClick={() => setTab(item)}
            role="tab"
            type="button"
          >
            {item}
          </button>
        ))}
      </div>
      {data && tab === "Membership" ? <MembershipForm tenantId={tenantId} value={data.membership} /> : null}
      {data && tab === "Freeze & Extensions" ? <FreezeForm tenantId={tenantId} value={data.freeze} /> : null}
      {data && tab === "Attendance" ? <AttendanceForm tenantId={tenantId} value={data.attendance} /> : null}
      {data && tab === "CRM" ? (
        <div>
          <CrmDefaultsForm tenantId={tenantId} value={data.crm} />
          <h2 className="mt-8 text-xl font-semibold">Lead sources</h2>
          <CatalogList items={data.leadSources} onAdd={() => setSourceDialog(true)} path="/configuration/crm/lead-sources" tenantId={tenantId} />
          <h2 className="mt-8 text-xl font-semibold">Lost reasons</h2>
          <CatalogList items={data.lostReasons} onAdd={() => setReasonDialog(true)} path="/configuration/crm/lost-reasons" tenantId={tenantId} />
        </div>
      ) : null}
      {tab === "PT Packages" ? <PtPackagesSection tenantId={tenantId} /> : null}
      {tab === "Coach Rewards" ? <CoachRewardsSection tenantId={tenantId} /> : null}
      {chatEnabled && tab === "Chat" ? <ChatPermissionsSection /> : null}
      {tab === "Roles & Permissions" ? <RolesPermissionsSection tenantId={tenantId} /> : null}
      {data && tab === "Member Categories" ? (
        <div className="mt-4">
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setCategoryDialog(true)} type="button">Add category</button>
          <div className="mt-4">
            <DataTable
              columns={[
                { header: "Name", cell: (row) => row.name },
                { header: "Description", cell: (row) => row.description || "—" },
                { header: "Status", cell: (row) => row.isActive ? "Active" : "Inactive" },
                { header: "Actions", cell: (row) => (
                  <div className="flex flex-wrap gap-2">
                    <button className="min-h-11 rounded-full border border-line px-3" onClick={() => setCategoryDialog(row)} type="button">Edit</button>
                    <button className="min-h-11 rounded-full border border-line px-3" onClick={() => categoryStatus.mutate({ id: row.id, active: !row.isActive })} type="button">{row.isActive ? "Deactivate" : "Activate"}</button>
                  </div>
                ) },
              ]}
              empty="No member categories yet."
              rowKey={(row) => row.id}
              rows={data.memberCategories}
            />
          </div>
        </div>
      ) : null}
      {data && tab === "Discounts" ? (
        <div className="mt-4">
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setDiscountDialog(true)} type="button">Add discount</button>
          <div className="mt-4">
            <DataTable
              columns={[
                { header: "Name", cell: (row) => row.name },
                { header: "Type", cell: (row) => row.discountType },
                { header: "Value", cell: (row) => row.discountType === "Percentage" ? `${row.value}%` : String(row.value) },
                { header: "Applies to", cell: (row) => row.appliesTo },
                { header: "Valid", cell: (row) => `${row.startDate || "—"} → ${row.endDate || "—"}` },
                { header: "Status", cell: (row) => row.isActive ? "Active" : "Inactive" },
                { header: "Actions", cell: (row) => (
                  <div className="flex flex-wrap gap-2">
                    <button className="min-h-11 rounded-full border border-line px-3" onClick={() => setDiscountDialog(row)} type="button">Edit</button>
                    <button className="min-h-11 rounded-full border border-line px-3" onClick={() => discountStatus.mutate({ id: row.id, active: !row.isActive })} type="button">{row.isActive ? "Deactivate" : "Activate"}</button>
                  </div>
                ) },
              ]}
              empty="No discount rules yet."
              rowKey={(row) => row.id}
              rows={data.discounts}
            />
          </div>
        </div>
      ) : null}
      {categoryDialog ? (
        <CategoryDialog
          initial={categoryDialog === true ? undefined : categoryDialog}
          onCancel={() => setCategoryDialog(null)}
          onSaved={async () => { setCategoryDialog(null); await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); }}
        />
      ) : null}
      {discountDialog && data ? (
        <DiscountDialog
          categories={data.memberCategories}
          initial={discountDialog === true ? undefined : discountDialog}
          onCancel={() => setDiscountDialog(null)}
          onSaved={async () => { setDiscountDialog(null); await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); }}
          packages={packages.data?.items ?? []}
        />
      ) : null}
      {sourceDialog ? <CatalogAddDialog onCancel={() => setSourceDialog(false)} onSaved={async () => { setSourceDialog(false); await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); }} path="/configuration/crm/lead-sources" title="Add lead source" /> : null}
      {reasonDialog ? <CatalogAddDialog onCancel={() => setReasonDialog(false)} onSaved={async () => { setReasonDialog(false); await client.invalidateQueries({ queryKey: ["configuration", tenantId] }); }} path="/configuration/crm/lost-reasons" title="Add lost reason" /> : null}
    </PageState>
  );
}
