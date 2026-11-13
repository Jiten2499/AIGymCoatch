"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { FormDialog } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { getSession } from "../../../lib/session";
import { emptyToNull, type BranchOption, type GymMemberDetails, type MembershipPackage } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { emptyDraft, splitFullName, stepError, type MemberDraft } from "./memberWizard";
import { MemberReview, MembershipPackageStep, type MembershipTerm, type PtPackageOption } from "./MemberPackageStep";
import { BasicStep, ContactStep, GymStep } from "./MemberWizardSteps";

type TrainerOption = { id: string; firstName: string; lastName: string; status: string };

export function AddMemberDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: () => void }) {
  const today = new Date().toISOString().slice(0, 10);
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<MemberDraft>(() => emptyDraft(today));
  const [photo, setPhoto] = useState<File | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [issuedPassword, setIssuedPassword] = useState<string | null>(null);
  const packages = useQuery({ queryKey: ["membership-plans", "active"], queryFn: () => api<Page<MembershipPackage>>("/membership-plans?activeOnly=true&page=1&pageSize=50") });
  const branches = useQuery({ queryKey: ["branches", "member-wizard"], queryFn: () => api<Page<BranchOption>>("/branches?page=1&pageSize=50") });
  const trainers = useQuery({ queryKey: ["trainers", "member-wizard"], queryFn: () => api<Page<TrainerOption>>("/trainers?page=1&pageSize=50") });
  const ptPackages = useQuery({ queryKey: ["pt-packages", "member-wizard"], queryFn: () => api<PtPackageOption[]>("/pt-packages?activeOnly=true") });
  const term = useQuery({
    queryKey: ["membership-term", draft.planId, draft.startDate],
    enabled: Boolean(draft.planId && draft.startDate),
    queryFn: () => api<MembershipTerm>(`/membership-plans/${draft.planId}/term?start=${draft.startDate}`),
  });
  const create = useMutation({
    mutationFn: async () => {
      const name = splitFullName(draft.fullName);
      if (!name) {
        throw new ApiError("Enter a first and last name.", 400);
      }
      const member = await api<GymMemberDetails>("/members", {
        method: "POST",
        body: JSON.stringify({
          email: emptyToNull(draft.email),
          password: null,
          firstName: name.firstName,
          lastName: name.lastName,
          phone: draft.phone.trim(),
          branchId: draft.planId ? emptyToNull(draft.branchId) : null,
          planId: draft.planId || null,
          startDate: draft.planId ? draft.startDate : null,
          autoRenew: false,
          dateOfBirth: emptyToNull(draft.dateOfBirth),
          gender: emptyToNull(draft.gender),
          emergencyContactName: emptyToNull(draft.emergencyName),
          emergencyContactPhone: emptyToNull(draft.emergencyPhone),
          emergencyContactRelationship: emptyToNull(draft.emergencyRelationship),
          address: emptyToNull(draft.address),
          city: emptyToNull(draft.city),
          state: emptyToNull(draft.state),
          postalCode: emptyToNull(draft.postalCode),
          joinDate: draft.joinDate,
          fitnessGoal: emptyToNull(draft.fitnessGoal),
          assignedTrainerId: draft.trainerId || null,
          status: draft.status,
          enableBasicAppAccess: draft.enableBasicAppAccess || draft.assignAppAccess || draft.sendActivationLink,
          assignAppAccess: draft.assignAppAccess,
          sendActivationLink: draft.sendActivationLink,
          appSubscriptionId: draft.assignAppAccess ? draft.appSubscriptionId || null : null,
          paymentAmount: draft.planId && draft.paymentAmount.trim() ? Number(draft.paymentAmount) : null,
          paymentMethod: draft.planId && draft.paymentAmount.trim() ? draft.paymentMethod : null,
          paymentDate: draft.planId && draft.paymentAmount.trim() ? draft.paymentDate || null : null,
          paymentReferenceNumber: draft.planId ? emptyToNull(draft.paymentReference) : null,
          paymentNotes: draft.planId ? emptyToNull(draft.paymentNotes) : null,
          ptPackageId: draft.ptPackageId || null,
        }),
      });
      if (photo) {
        const body = new FormData();
        body.append("file", photo);
        const session = getSession();
        const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL ?? "http://127.0.0.1:8080/api/v1"}/members/${member.id}/profile-photo`, {
          method: "POST",
          headers: session ? { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" } : { Accept: "application/json" },
          body,
        });
        if (!response.ok) {
          throw new ApiError("The member was saved, but the photo was not.", response.status);
        }
      }
      return member;
    },
    onSuccess: (member) => {
      if (member.temporaryPassword) {
        setIssuedPassword(member.temporaryPassword);
        return;
      }
      onCreated();
    },
  });
  const selected = (packages.data?.items ?? []).find((plan) => plan.id === draft.planId) ?? null;
  const selectedPt = (ptPackages.data ?? []).find((item) => item.id === draft.ptPackageId) ?? null;
  const activeTrainers = (trainers.data?.items ?? []).filter((trainer) => trainer.status === "Active");
  const trainerName = activeTrainers.find((trainer) => trainer.id === draft.trainerId);

  function update(patch: Partial<MemberDraft>) {
    setMessage(null);
    setDraft((current) => ({ ...current, ...patch }));
  }

  function next() {
    const error = stepError(step, draft);
    if (error) {
      setMessage(error);
      return;
    }
    setMessage(null);
    setStep((current) => current + 1);
  }

  return (
    <FormDialog title={issuedPassword ? "Member saved" : `Add member · Step ${step} of 5`} onCancel={issuedPassword ? onCreated : onCancel} wide>
      <div className="mt-4 grid gap-3">
        {issuedPassword ? (
          <>
            <p>A sign-in was created for this member. Share this temporary password once. It is not shown again.</p>
            <p className="rounded-xl border border-line px-4 py-3 font-mono">{issuedPassword}</p>
            <div className="mt-2 flex justify-end">
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={onCreated} type="button">Done</button>
            </div>
          </>
        ) : null}
        {issuedPassword ? null : <>
        {step === 1 ? <BasicStep draft={draft} onPhoto={setPhoto} photoName={photo?.name ?? ""} update={update} /> : null}
        {step === 2 ? <ContactStep draft={draft} update={update} /> : null}
        {step === 3 ? (
          <GymStep
            draft={draft}
            trainers={activeTrainers}
            update={update}
          />
        ) : null}
        {step === 4 ? (
          <MembershipPackageStep
            branches={branches.data?.items ?? []}
            draft={draft}
            plans={packages.data?.items ?? []}
            ptPackages={ptPackages.data ?? []}
            term={term.data}
            update={update}
          />
        ) : null}
        {step === 5 ? (
          <>
            <MemberReview draft={draft} plan={selected} ptPackage={selectedPt} term={term.data ?? null} trainerName={trainerName ? `${trainerName.firstName} ${trainerName.lastName}` : ""} />
            <label className="flex min-h-11 items-center gap-3 text-sm">
              <input checked={draft.sendActivationLink} className="size-4" onChange={(event) => setDraft((current) => ({ ...current, sendActivationLink: event.target.checked }))} type="checkbox" />
              Send activation link
            </label>
          </>
        ) : null}
        {message ? <p role="alert">{message}</p> : null}
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <div className="mt-2 flex items-center justify-between gap-3">
          <button className="min-h-11 rounded-full border border-line px-4" disabled={create.isPending} onClick={onCancel} type="button">Cancel</button>
          <div className="flex gap-3">
            {step > 1 ? <button className="min-h-11 rounded-full px-4" disabled={create.isPending} onClick={() => setStep((current) => current - 1)} type="button">Previous</button> : null}
            {step < 5 ? (
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={next} type="button">Next</button>
            ) : (
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={create.isPending} onClick={() => { const error = stepError(4, draft) ?? stepError(3, draft) ?? stepError(1, draft); if (error) { setMessage(error); return; } if (draft.sendActivationLink && !draft.email.trim()) { setMessage("Enter an email before sending an activation link."); return; } create.mutate(); }} type="button">{create.isPending ? "Saving..." : "Save member"}</button>
            )}
          </div>
        </div>
        </>}
      </div>
    </FormDialog>
  );
}
