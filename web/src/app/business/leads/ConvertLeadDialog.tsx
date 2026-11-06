"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { AppAccessFields } from "../../../components/AppAccessFields";
import { FormDialog } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { emptyToNull, type BranchOption, type LeadDetails, type MembershipPackage } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { convertStepError, draftFromLead, fitnessGoals, memberGenders, splitFullName, type ConvertDraft, type MemberMatch } from "./convertWizard";

type TrainerOption = { id: string; firstName: string; lastName: string; status: string };
type MembershipTerm = { startDate: string; endDate: string; status: string; price: number; currency: string };

export function ConvertLeadDialog({ lead, onCancel, onConverted }: { lead: LeadDetails; onCancel: () => void; onConverted: () => void }) {
  const today = new Date().toISOString().slice(0, 10);
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<ConvertDraft>(() => draftFromLead(lead, today));
  const [message, setMessage] = useState<string | null>(null);
  const [matches, setMatches] = useState<MemberMatch[]>([]);
  const packages = useQuery({ queryKey: ["membership-plans", "convert"], queryFn: () => api<Page<MembershipPackage>>("/membership-plans?activeOnly=true&page=1&pageSize=50") });
  const branches = useQuery({ queryKey: ["branches", "convert"], queryFn: () => api<Page<BranchOption>>("/branches?page=1&pageSize=50") });
  const trainers = useQuery({ queryKey: ["trainers", "convert"], queryFn: () => api<Page<TrainerOption>>("/trainers?page=1&pageSize=50") });
  const term = useQuery({
    queryKey: ["membership-term", draft.planId, draft.startDate],
    enabled: Boolean(draft.planId && draft.startDate),
    queryFn: () => api<MembershipTerm>(`/membership-plans/${draft.planId}/term?start=${draft.startDate}`),
  });
  const convert = useMutation({
    mutationFn: async () => {
      const name = splitFullName(draft.fullName);
      if (!name) {
        throw new ApiError("Enter a first and last name.", 400);
      }
      return api<LeadDetails>(`/leads/${lead.id}/convert`, {
        method: "POST",
        body: JSON.stringify({
          memberProfileId: draft.createNew ? null : draft.memberProfileId,
          firstName: name.firstName,
          lastName: name.lastName,
          phone: draft.phone.trim(),
          email: emptyToNull(draft.email),
          dateOfBirth: emptyToNull(draft.dateOfBirth),
          gender: emptyToNull(draft.gender),
          address: emptyToNull(draft.address),
          emergencyContactName: emptyToNull(draft.emergencyName),
          emergencyContactPhone: emptyToNull(draft.emergencyPhone),
          joinDate: draft.joinDate,
          fitnessGoal: emptyToNull(draft.fitnessGoal),
          assignedTrainerId: emptyToNull(draft.trainerId),
          planId: draft.planId || null,
          branchId: emptyToNull(draft.branchId),
          startDate: draft.planId ? draft.startDate : null,
          enableBasicAppAccess: draft.enableBasicAppAccess || draft.assignAppAccess,
          assignAppAccess: draft.assignAppAccess,
          appSubscriptionId: draft.assignAppAccess ? draft.appSubscriptionId || null : null,
        }),
      });
    },
    onSuccess: () => onConverted(),
  });

  function update(patch: Partial<ConvertDraft>) {
    setDraft((current) => ({ ...current, ...patch }));
  }

  async function next() {
    const error = convertStepError(step, draft);
    if (error) {
      setMessage(error);
      return;
    }
    setMessage(null);
    if (step === 1) {
      const found = await api<MemberMatch[]>(`/leads/${lead.id}/member-matches?mobile=${encodeURIComponent(draft.phone.trim())}${draft.email.trim() ? `&email=${encodeURIComponent(draft.email.trim())}` : ""}`);
      setMatches(found);
      if (found.length > 0) {
        setStep(2);
        return;
      }
      update({ createNew: true, memberProfileId: null });
      setStep(3);
      return;
    }
    setStep((current) => current + 1);
  }

  const selectedPlan = (packages.data?.items ?? []).find((plan) => plan.id === draft.planId);
  const activeTrainers = (trainers.data?.items ?? []).filter((trainer) => trainer.status === "Active");

  return (
    <FormDialog title={step === 4 ? "Review conversion" : `Convert lead · Step ${step} of 4`} onCancel={onCancel} wide>
      <div className="mt-4 grid gap-3">
        {step === 1 ? (
          <>
            <p className="text-muted">Source {draft.source}. Notes: {draft.notes || "None"}</p>
            <Label text="Full name"><input className="field" onChange={(event) => update({ fullName: event.target.value })} value={draft.fullName} /></Label>
            <Label text="Mobile"><input className="field" onChange={(event) => update({ phone: event.target.value })} value={draft.phone} /></Label>
            <Label text="Email"><input className="field" onChange={(event) => update({ email: event.target.value })} value={draft.email} /></Label>
            <Label text="Date of birth"><input className="field" onChange={(event) => update({ dateOfBirth: event.target.value })} type="date" value={draft.dateOfBirth} /></Label>
            <Label text="Gender">
              <select className="field" onChange={(event) => update({ gender: event.target.value })} value={draft.gender}>
                <option value="">Prefer not to say</option>
                {memberGenders.map((gender) => <option key={gender} value={gender}>{gender}</option>)}
              </select>
            </Label>
            <Label text="Address"><input className="field" onChange={(event) => update({ address: event.target.value })} value={draft.address} /></Label>
            <Label text="Emergency contact"><input className="field" onChange={(event) => update({ emergencyName: event.target.value })} value={draft.emergencyName} /></Label>
            <Label text="Emergency mobile"><input className="field" onChange={(event) => update({ emergencyPhone: event.target.value })} value={draft.emergencyPhone} /></Label>
            <Label text="Joining date"><input className="field" onChange={(event) => update({ joinDate: event.target.value })} type="date" value={draft.joinDate} /></Label>
            <Label text="Fitness goal">
              <select className="field" onChange={(event) => update({ fitnessGoal: event.target.value })} value={draft.fitnessGoal}>
                <option value="">None</option>
                {fitnessGoals.map((goal) => <option key={goal} value={goal}>{goal}</option>)}
              </select>
            </Label>
            <Label text="Assigned trainer">
              <select className="field" onChange={(event) => update({ trainerId: event.target.value })} value={draft.trainerId}>
                <option value="">None</option>
                {activeTrainers.map((trainer) => <option key={trainer.id} value={trainer.id}>{trainer.firstName} {trainer.lastName}</option>)}
              </select>
            </Label>
            <p className="text-muted">Basic LifeFit login is optional and does not use a seat. An optional App Plan seat can be added for premium features.</p>
          </>
        ) : null}
        {step === 2 ? (
          <>
            <p>An existing member may already exist for this lead.</p>
            {matches.map((match) => (
              <button
                className={`rounded-xl border px-4 py-3 text-left ${draft.memberProfileId === match.id && !draft.createNew ? "border-accent" : "border-line"}`}
                key={match.id}
                onClick={() => update({ createNew: false, memberProfileId: match.id })}
                type="button"
              >
                <span className="font-semibold">{match.fullName}</span>
                <span className="mt-1 block text-muted">{match.memberCode} · {match.phone || "No phone"} · {match.email || "No email"} · {match.status}</span>
              </button>
            ))}
            <button className={`min-h-11 rounded-full border px-4 ${draft.createNew ? "border-accent" : "border-line"}`} onClick={() => update({ createNew: true, memberProfileId: null })} type="button">Create New Member</button>
            <button className="min-h-11 rounded-full border border-line px-4" disabled={!draft.memberProfileId || draft.createNew} onClick={() => setStep(3)} type="button">Link Existing Member</button>
          </>
        ) : null}
        {step === 3 ? (
          <>
            <Label text="Membership package">
              <select className="field" onChange={(event) => update({ planId: event.target.value })} value={draft.planId}>
                <option value="">No Membership</option>
                {(packages.data?.items ?? []).map((plan) => <option key={plan.id} value={plan.id}>{plan.name} · {plan.price} {plan.currency}</option>)}
              </select>
            </Label>
            {draft.planId ? (
              <>
                <Label text="Branch">
                  <select className="field" onChange={(event) => update({ branchId: event.target.value })} value={draft.branchId}>
                    <option value="">None</option>
                    {(branches.data?.items ?? []).map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
                  </select>
                </Label>
                <Label text="Membership start date"><input className="field" onChange={(event) => update({ startDate: event.target.value })} type="date" value={draft.startDate} /></Label>
                <p>Expiry {term.data?.endDate ?? "Calculating..."}</p>
                <p>Membership status {term.data?.status ?? ""}</p>
              </>
            ) : <p className="text-muted">No membership package selected.</p>}
            <AppAccessFields
              draft={{ enableBasicAppAccess: draft.enableBasicAppAccess, assignAppAccess: draft.assignAppAccess, appSubscriptionId: draft.appSubscriptionId }}
              onChange={(patch) => update(patch)}
              requireEmailHint
            />
          </>
        ) : null}
        {step === 4 ? (
          <>
            <h3 className="font-semibold">Lead</h3>
            <p>{draft.fullName} · {draft.phone} · {draft.email || "No email"} · {draft.source}</p>
            <h3 className="mt-2 font-semibold">Member</h3>
            <p>{draft.createNew ? "Create new member" : "Link existing member"} · {draft.fullName} · {draft.phone}</p>
            <h3 className="mt-2 font-semibold">Membership</h3>
            <p>{selectedPlan ? `${selectedPlan.name} · ${draft.startDate} → ${term.data?.endDate ?? "…"} · ${term.data?.status ?? ""}` : "No membership package selected"}</p>
            <h3 className="mt-2 font-semibold">LifeFit App</h3>
            <p>Basic login: {draft.enableBasicAppAccess || draft.assignAppAccess ? "Yes" : "No"}</p>
            <p>App plan seat: {draft.assignAppAccess ? "Yes" : "No"}</p>
          </>
        ) : null}
        {message ? <p role="alert">{message}</p> : null}
        {convert.error instanceof ApiError ? <p role="alert">{convert.error.message}</p> : null}
        <div className="mt-2 flex items-center justify-between gap-3">
          <button className="min-h-11 rounded-full border border-line px-4" disabled={convert.isPending} onClick={onCancel} type="button">Cancel</button>
          <div className="flex gap-3">
            {step > 1 ? <button className="min-h-11 rounded-full px-4" disabled={convert.isPending} onClick={() => setStep((current) => (current === 3 && matches.length === 0 ? 1 : current - 1))} type="button">Previous</button> : null}
            {step < 4 ? (
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => void next()} type="button">Next</button>
            ) : (
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={convert.isPending} onClick={() => { const error = convertStepError(1, draft) ?? convertStepError(3, draft); if (error) { setMessage(error); return; } convert.mutate(); }} type="button">{convert.isPending ? "Converting..." : "Convert to Member"}</button>
            )}
          </div>
        </div>
      </div>
    </FormDialog>
  );
}

function Label({ text, children }: { text: string; children: React.ReactNode }) {
  return <label className="grid gap-1 text-sm">{text}{children}</label>;
}
