"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "../lib/api";
import type { WorkspacePlan } from "../lib/types";

export type AppAccessDraft = {
  enableBasicAppAccess: boolean;
  assignAppAccess: boolean;
  appSubscriptionId: string;
};

export function emptyAppAccess(): AppAccessDraft {
  return { enableBasicAppAccess: false, assignAppAccess: false, appSubscriptionId: "" };
}

export function appAccessPayload(draft: AppAccessDraft) {
  return {
    enableBasicAppAccess: draft.enableBasicAppAccess,
    assignAppAccess: draft.assignAppAccess,
    appSubscriptionId: draft.assignAppAccess && draft.appSubscriptionId ? draft.appSubscriptionId : null,
  };
}

export function appAccessError(draft: AppAccessDraft, email?: string): string | null {
  if ((draft.enableBasicAppAccess || draft.assignAppAccess) && email !== undefined && !email.trim()) {
    return "Enter an email for LifeFit app login.";
  }
  if (draft.assignAppAccess && !draft.appSubscriptionId) {
    return "Select a LifeFit app plan.";
  }
  return null;
}

export function AppAccessFields({
  draft,
  onChange,
  tenantId,
  requireEmailHint,
}: {
  draft: AppAccessDraft;
  onChange: (patch: Partial<AppAccessDraft>) => void;
  tenantId?: string | null;
  requireEmailHint?: boolean;
}) {
  const plans = useQuery({
    queryKey: ["workspace-plans", tenantId ?? "current"],
    queryFn: () => api<WorkspacePlan[]>("/business/plans"),
  });
  const usable = (plans.data ?? []).filter((plan) => plan.usable);
  const selected = usable.find((plan) => plan.subscriptionId === draft.appSubscriptionId);

  return (
    <section className="grid gap-4 rounded-xl border border-line p-4">
      <div>
        <h3 className="font-semibold">LifeFit App Access</h3>
        <p className="text-sm text-muted">Basic login is free for every gym member. An optional app plan seat unlocks premium features such as AI.</p>
      </div>

      <div className="grid gap-2">
        <label className="flex items-center gap-2 text-sm">
          <input
            checked={draft.enableBasicAppAccess || draft.assignAppAccess}
            onChange={(event) => {
              const enabled = event.target.checked;
              onChange({
                enableBasicAppAccess: enabled,
                assignAppAccess: enabled ? draft.assignAppAccess : false,
                appSubscriptionId: enabled ? draft.appSubscriptionId : "",
              });
            }}
            type="checkbox"
          />
          Give this member basic LifeFit app login
        </label>
        {requireEmailHint && (draft.enableBasicAppAccess || draft.assignAppAccess) ? (
          <p className="text-sm text-muted">An email is required for basic LifeFit login. No tenant seat is used.</p>
        ) : null}
      </div>

      {(draft.enableBasicAppAccess || draft.assignAppAccess) ? (
        <div className="grid gap-3 border-t border-line pt-3">
          <label className="flex items-center gap-2 text-sm">
            <input
              checked={draft.assignAppAccess}
              onChange={(event) => onChange({
                assignAppAccess: event.target.checked,
                enableBasicAppAccess: true,
                appSubscriptionId: event.target.checked ? draft.appSubscriptionId : "",
              })}
              type="checkbox"
            />
            Also assign a LifeFit App Plan (uses 1 seat)
          </label>
          {draft.assignAppAccess ? (
            <>
              <label className="grid gap-2 text-sm">
                App plan
                <select
                  aria-label="LifeFit app plan"
                  className="field"
                  onChange={(event) => onChange({ appSubscriptionId: event.target.value })}
                  value={draft.appSubscriptionId}
                >
                  <option value="">Select plan</option>
                  {usable.map((plan) => (
                    <option key={plan.subscriptionId} value={plan.subscriptionId}>
                      {plan.planName} · {plan.available} seats available · {plan.features.filter((feature) => feature.enabled).length} features
                    </option>
                  ))}
                </select>
              </label>
              {selected ? (
                <p className="text-sm">
                  Purchased {selected.seats} · Assigned {selected.assigned} · Available {selected.available}
                </p>
              ) : null}
              {selected && selected.available <= 0 ? <p role="alert">No seats available for this plan.</p> : null}
              <p className="text-sm text-muted">Premium app features require one seat from your tenant&apos;s LifeFit license. Gym membership stays separate.</p>
            </>
          ) : (
            <p className="text-sm text-muted">Member can sign in to the LifeFit app without a paid seat.</p>
          )}
        </div>
      ) : null}
    </section>
  );
}
