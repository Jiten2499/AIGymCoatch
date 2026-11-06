"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import type { BusinessConfiguration } from "../../../lib/configuration";
import type { GymMembership } from "../../../lib/gym";
import { addDays, freezeFormError } from "./freezeRules";

export function FreezeDialog({ membership, tenantId, onCancel, onFrozen }: { membership: GymMembership; tenantId: string | null; onCancel: () => void; onFrozen: () => void }) {
  const config = useQuery({
    queryKey: ["configuration", tenantId, "freeze"],
    enabled: tenantId !== null,
    queryFn: () => api<BusinessConfiguration>("/configuration"),
  });
  const rules = config.data?.freeze;
  const minDays = rules?.minFreezeDays ?? 1;
  const maxDays = rules?.maxFreezeDays ?? 30;
  const [days, setDays] = useState("");
  useEffect(() => {
    if (rules && days === "") {
      setDays(String(Math.max(rules.minFreezeDays, 1)));
    }
  }, [days, rules]);
  const [reason, setReason] = useState("");
  const [queued, setQueued] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const parsed = Number(days);
  const preview = Number.isInteger(parsed) && parsed > 0 ? addDays(membership.endDate, parsed) : membership.endDate;
  const freeze = useMutation({
    mutationFn: () => api<GymMembership>(`/memberships/${membership.id}/freeze`, {
      method: "POST",
      body: JSON.stringify({ reason: reason.trim() || null, autoRenew: false, days: parsed }),
    }),
    onSuccess: (result) => {
      if (result.approvalRequestId) {
        setQueued(true);
        return;
      }
      onFrozen();
    },
  });

  return (
    <FormDialog title="Freeze membership" onCancel={onCancel}>
      <form
        className="mt-4 grid gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          if (queued) {
            onFrozen();
            return;
          }
          const error = freezeFormError(parsed, minDays, maxDays);
          if (error) {
            setMessage(error);
            return;
          }
          setMessage(null);
          freeze.mutate();
        }}
      >
        <p className="text-sm text-muted">{membership.memberName || membership.membershipNumber} · {membership.planName || membership.membershipNumber} · ends {membership.endDate}</p>
        <p className="text-sm text-muted">Allowed freeze length is {Math.max(minDays, 1)} to {maxDays} days, up to {rules?.maxFreezeOccurrences ?? 2} times. The membership end date moves out by the freeze length now.</p>
        <FormField label="Freeze days">
          <input className="field" min={1} onChange={(event) => setDays(event.target.value)} required type="number" value={days} />
        </FormField>
        <FormField label="Reason">
          <textarea className="field min-h-20 py-2" maxLength={500} onChange={(event) => setReason(event.target.value)} value={reason} />
        </FormField>
        <p className="text-sm">New end date {preview}</p>
        {rules?.freezeRequiresApproval ? <p className="text-sm">This freeze is sent for approval. The membership stays active until an approver accepts it.</p> : null}
        {queued ? <p role="status">Sent for approval. It is waiting in Tasks.</p> : null}
        {message ? <p role="alert">{message}</p> : null}
        {freeze.error instanceof ApiError ? <p role="alert">{freeze.error.message}</p> : null}
        {queued ? <FormActions cancel={onFrozen} pending={false} submitLabel="Close" /> : <FormActions cancel={onCancel} pending={freeze.isPending} submitLabel="Freeze" />}
      </form>
    </FormDialog>
  );
}
