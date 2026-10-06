"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { api, ApiError } from "../../lib/api";
import type { ExpiringLicense, LlmConnection, Page, Plan, TenantLicense } from "../../lib/types";
import { PageState } from "../../components/AdminShell";

export default function OverviewPage() {
  const plans = useQuery({ queryKey: ["plans"], queryFn: () => api<Plan[]>("/platform/plans") });
  const expiring = useQuery({ queryKey: ["expiring"], queryFn: () => api<ExpiringLicense[]>("/platform/licenses/expiring?withinDays=30") });
  const licenses = useQuery({ queryKey: ["licenses", 1], queryFn: () => api<Page<TenantLicense>>("/platform/licenses?page=1&pageSize=20") });
  const connections = useQuery({ queryKey: ["llm"], queryFn: () => api<LlmConnection[]>("/llm/connections") });
  const error = [plans.error, expiring.error, licenses.error, connections.error].find(Boolean);
  const loading = plans.isLoading || expiring.isLoading || licenses.isLoading || connections.isLoading;
  const active = connections.data?.find((connection) => connection.isDefault);

  return (
    <PageState error={error instanceof ApiError ? error.message : error ? "The overview could not be loaded." : null} loading={loading} onRetry={() => { void plans.refetch(); void expiring.refetch(); void licenses.refetch(); void connections.refetch(); }}>
      <h1 className="text-3xl font-semibold">Overview</h1>
      <p className="mt-2 text-muted">Counts below come from the current API responses. Tenant totals are not shown because the workspace list does not return a separate count.</p>
      <div className="mt-8 grid gap-4 md:grid-cols-2">
        <Card href="/admin/plans" label="Plans" value={String(plans.data?.length ?? 0)} />
        <Card href="/admin/expiring" label="Expiring within 30 days" value={String(expiring.data?.length ?? 0)} />
        <Card href="/admin/licenses" label="Subscriptions on this page" value={`${licenses.data?.items.length ?? 0} of ${licenses.data?.totalCount ?? 0}`} />
        <Card href="/admin/llm" label="Default connection" value={active ? `${active.name} · ${active.healthStatus}` : "None"} />
      </div>
    </PageState>
  );
}

function Card({ href, label, value }: { href: string; label: string; value: string }) {
  return (
    <Link className="rounded-2xl border border-line bg-surface p-5" href={href}>
      <p className="text-sm text-muted">{label}</p>
      <p className="mt-2 text-2xl font-semibold">{value}</p>
    </Link>
  );
}
