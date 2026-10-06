"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { api, ApiError } from "../../lib/api";
import type { Plan } from "../../lib/types";

export function AssignCatalogPlanDialog({
  title,
  description,
  audience,
  path,
  onCancel,
  onSaved,
}: {
  title: string;
  description: string;
  audience: "business" | "personal";
  path: string;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const plans = useQuery({ queryKey: ["plans"], queryFn: () => api<Plan[]>("/platform/plans") });
  const accepted = audience === "business" ? "Business" : "Personal";
  const choices = (plans.data ?? []).filter((plan) => plan.status === "Active" && (plan.appliesTo === accepted || plan.appliesTo === "Any"));
  const [picked, setPicked] = useState<string | null>(null);
  const planId = picked ?? choices[0]?.id ?? "";
  const assign = useMutation({
    mutationFn: () => api(path, { method: "POST", body: JSON.stringify({ planId }) }),
    onSuccess: () => onSaved(),
  });

  return (
    <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation">
      <div aria-labelledby="assign-plan-title" aria-modal="true" className="w-full max-w-lg rounded-2xl border border-line bg-surface p-6" role="dialog">
        <h2 className="text-xl font-semibold" id="assign-plan-title">{title}</h2>
        <p className="mt-3 text-muted">{description}</p>
        {plans.isLoading ? <p className="mt-4">Loading plans.</p> : null}
        {plans.error instanceof ApiError ? <p className="mt-4" role="alert">{plans.error.message}</p> : null}
        {!plans.isLoading && choices.length === 0 ? <p className="mt-4">No active plans apply here.</p> : null}
        {choices.length > 0 ? (
          <form className="mt-4 grid gap-3" onSubmit={(event) => { event.preventDefault(); assign.mutate(); }}>
            <label className="grid gap-2">
              Plan
              <select aria-label="Plan" className="field" onChange={(event) => setPicked(event.target.value)} value={planId}>
                {choices.map((plan) => <option key={plan.id} value={plan.id}>{plan.name} · {plan.baseCapacity} seats</option>)}
              </select>
            </label>
            {assign.error instanceof ApiError ? <p role="alert">{assign.error.message}</p> : null}
            <div className="mt-2 flex justify-end gap-3">
              <button className="min-h-11 rounded-full px-4" onClick={onCancel} type="button">Cancel</button>
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={assign.isPending || planId === ""} type="submit">
                {assign.isPending ? "Saving..." : "Assign plan"}
              </button>
            </div>
          </form>
        ) : (
          <div className="mt-6 flex justify-end">
            <button className="min-h-11 rounded-full px-4" onClick={onCancel} type="button">Close</button>
          </div>
        )}
      </div>
    </div>
  );
}
