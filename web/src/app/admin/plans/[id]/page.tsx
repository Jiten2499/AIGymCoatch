"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { PageState } from "../../../../components/AdminShell";
import { api, ApiError } from "../../../../lib/api";
import type { FeatureRow, PlanDetail } from "../../../../lib/types";

export default function PlanDetailPage() {
  const params = useParams<{ id: string }>();
  const client = useQueryClient();
  const plan = useQuery({ queryKey: ["plan", params.id], queryFn: () => api<PlanDetail>(`/platform/plans/${params.id}`) });
  const features = useQuery({ queryKey: ["features"], queryFn: () => api<FeatureRow[]>("/platform/features") });
  const setFeature = useMutation({
    mutationFn: (input: { code: string; enabled: boolean }) => api(`/platform/plans/${params.id}/features`, { method: "PUT", body: JSON.stringify({ featureCode: input.code, enabled: input.enabled }) }),
    onSuccess: async () => client.invalidateQueries({ queryKey: ["plan", params.id] }),
  });
  const error = plan.error ?? features.error ?? setFeature.error;
  const enabled = new Map((plan.data?.features ?? []).map((feature) => [feature.featureCode, feature.enabled]));

  return (
    <PageState error={plan.isError || features.isError ? (error instanceof ApiError ? error.message : "The plan could not be loaded.") : null} loading={plan.isLoading || features.isLoading} onRetry={() => { void plan.refetch(); void features.refetch(); }}>
      <h1 className="text-3xl font-semibold">{plan.data?.name}</h1>
      <p className="mt-2 text-muted">{plan.data?.code} · {plan.data?.status} · {plan.data?.baseCapacity} seats · {plan.data?.description || "No description"}</p>
      {error instanceof ApiError && !plan.isError ? <p className="mt-4" role="alert">{error.message}</p> : null}
      <h2 className="mt-8 text-xl font-semibold">Features</h2>
      <p className="mt-2 text-muted">A subscriber receives every enabled feature on this plan. There are no per-workspace overrides.</p>
      <ul className="mt-4 grid gap-2">
        {(features.data ?? []).map((feature) => (
          <li key={feature.code}>
            <label className="flex min-h-11 items-center gap-3">
              <input checked={enabled.get(feature.code) === true} disabled={!feature.isActive || setFeature.isPending} onChange={(event) => setFeature.mutate({ code: feature.code, enabled: event.target.checked })} type="checkbox" />
              <span>{feature.name}</span>
              <span className="text-muted">{feature.code} · {feature.scope}</span>
            </label>
          </li>
        ))}
      </ul>
    </PageState>
  );
}
