"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { FormDialog } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { saveCompensation } from "../../../lib/payroll";
import { getSession, subscribeSession } from "../../../lib/session";
import type { CreatedCoach, Page, WorkspacePlan, WorkspaceUser } from "../../../lib/types";
import {
  AccessReviewStep,
  CompensationStep,
  CoachStepIndicator,
  EmploymentStep,
  PersonalStep,
  ProfessionalStep,
  type BranchOption,
  type Candidate,
} from "./AddCoachWizardSteps";
import { coachPhotoError, uploadCoachPhoto } from "./CoachPhotoField";
import {
  certificationText,
  commissionEligibleFor,
  draftIsDirty,
  employmentNotes,
  emptyCoachDraft,
  needsMonthlySalary,
  needsPerSessionRate,
  resolvedFirstName,
  specializationText,
  stepError,
  type CoachDraft,
} from "./coachWizard";

export type CoachWizardResult = {
  coach: CreatedCoach;
  photoWarning?: string;
  compensationWarning?: string;
  activationQueued?: boolean;
};

const wizardSteps = ["Personal", "Professional", "Employment", "Compensation", "Access"] as const;

export function AddCoachWizard({
  onCancel,
  onCreated,
  canEditCompensation,
}: {
  onCancel: () => void;
  onCreated: (result: CoachWizardResult) => void;
  canEditCompensation: boolean;
}) {
  const today = new Date().toISOString().slice(0, 10);
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<CoachDraft>(() => emptyCoachDraft(today));
  const [photo, setPhoto] = useState<File | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [candidate, setCandidate] = useState<Candidate | null>(null);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const branches = useQuery({ queryKey: ["branches", "coach-wizard"], queryFn: () => api<Page<BranchOption>>("/branches?page=1&pageSize=100") });
  const plans = useQuery({ queryKey: ["workspace-plans", "coach-wizard"], queryFn: () => api<WorkspacePlan[]>("/business/plans") });
  const managers = useQuery({
    queryKey: ["workspace-users", "coach-managers", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<WorkspaceUser>>("/business/users?page=1&pageSize=100"),
  });
  const activeBranches = (branches.data?.items ?? []).filter((branch) => !branch.status || branch.status === "Active");
  const managerRows = (managers.data?.items ?? []).filter((row) => row.role !== "Trainer");
  const seatChoices = (plans.data ?? []).filter((plan) => plan.usable && plan.available > 0);

  const create = useMutation({
    mutationFn: async () => {
      const picture = coachPhotoError(photo);
      if (picture) {
        throw new ApiError(picture, 400);
      }
      const coach = await api<CreatedCoach>("/trainers", {
        method: "POST",
        body: JSON.stringify({
          email: draft.email.trim().toLowerCase(),
          firstName: resolvedFirstName(draft),
          lastName: draft.lastName.trim(),
          phone: draft.phone.trim(),
          specialization: specializationText(draft) || null,
          branchId: draft.branchId || null,
          sendActivationLink: draft.sendActivationLink,
          dateOfBirth: draft.dateOfBirth || null,
          gender: draft.gender || null,
          address: draft.address.trim() || null,
          emergencyContactName: draft.emergencyName.trim() || null,
          emergencyContactPhone: draft.emergencyPhone.trim() || null,
          designation: draft.designation.trim(),
          bio: draft.bio.trim() || null,
          certification: certificationText(draft) || null,
          experienceYears: draft.experienceYears.trim() ? Number(draft.experienceYears) : null,
          employmentType: draft.employmentType || null,
          joiningDate: draft.joiningDate || null,
          reportingManagerUserId: draft.reportingManagerUserId || null,
          employmentNotes: employmentNotes(draft) || null,
          status: draft.employmentStatus === "Inactive" ? "Inactive" : "Active",
          commissionEligible: draft.commissionEligible || commissionEligibleFor(draft.salaryType),
          subscriptionId: draft.subscriptionId || null,
        }),
      });

      let photoWarning: string | undefined;
      let compensationWarning: string | undefined;
      if (photo) {
        try {
          await uploadCoachPhoto(coach.id, photo);
        } catch (error) {
          photoWarning = error instanceof ApiError ? error.message : "The coach was created, but the photo was not.";
        }
      }

      if (canEditCompensation && coach.staffProfileId) {
        const baseSalary = needsMonthlySalary(draft.salaryType) && draft.monthlySalary.trim() ? Number(draft.monthlySalary) : 0;
        const allowances = needsPerSessionRate(draft.salaryType) && draft.perSessionRate.trim() ? Number(draft.perSessionRate) : 0;
        if ((baseSalary > 0 || allowances > 0 || employmentNotes(draft)) && !Number.isNaN(baseSalary) && !Number.isNaN(allowances)) {
          try {
            await saveCompensation(coach.staffProfileId, baseSalary, allowances, draft.joiningDate || today, employmentNotes(draft) || null);
          } catch (error) {
            compensationWarning = error instanceof ApiError ? error.message : "The coach was created, but compensation was not saved.";
          }
        }
      }

      return { coach, photoWarning, compensationWarning, activationQueued: draft.sendActivationLink || Boolean(candidate && !candidate.alreadyInWorkspace) };
    },
    onSuccess: (result) => onCreated(result),
  });

  function update(patch: Partial<CoachDraft>) {
    setMessage(null);
    setDraft((current) => ({ ...current, ...patch }));
  }

  async function lookupIdentity() {
    setMessage(null);
    try {
      const match = (await api<Candidate | null | undefined>(
        `/business/users/lookup?email=${encodeURIComponent(draft.email.trim())}&phone=${encodeURIComponent(draft.phone.trim())}`,
      )) ?? null;
      setCandidate(match);
      if (match?.alreadyInWorkspace) {
        setMessage("That person already has access to this workspace.");
        return false;
      }
      if (match) {
        update({
          firstName: draft.firstName || match.firstName,
          lastName: draft.lastName || match.lastName,
          phone: draft.phone || match.phone || "",
          linkExistingUser: true,
          sendActivationLink: true,
        });
      } else {
        update({ linkExistingUser: false });
      }
      return true;
    } catch (error) {
      // Lookup is advisory; a missing email should continue as a new account.
      setCandidate(null);
      update({ linkExistingUser: false });
      if (error instanceof ApiError && (error.status === 403 || error.status === 401)) {
        setMessage(error.message);
        return false;
      }
      return true;
    }
  }

  async function goNext() {
    const error = stepError(step, draft, { requireCompensation: canEditCompensation && step === 4 });
    if (error) {
      setMessage(error);
      return;
    }
    if (step === 1) {
      const ok = await lookupIdentity();
      if (!ok) return;
    }
    if (step === 3 && !canEditCompensation) {
      setStep(5);
      return;
    }
    setStep((current) => Math.min(5, current + 1));
  }

  function goPrevious() {
    setMessage(null);
    if (step === 5 && !canEditCompensation) {
      setStep(3);
      return;
    }
    setStep((current) => Math.max(1, current - 1));
  }

  function nextLabel() {
    if (step === 3 && !canEditCompensation) return "Access";
    return wizardSteps[step] ?? "Review";
  }

  function requestClose() {
    if (draftIsDirty(draft, today) || photo) {
      setConfirmCancel(true);
      return;
    }
    onCancel();
  }

  return (
    <>
      <FormDialog
        large
        header={(
          <div className="flex items-start justify-between gap-4">
            <div>
              <h2 className="text-xl font-semibold" id="form-dialog-title">Add New Coach</h2>
              <p className="mt-1 text-sm text-muted">Add a coach to your team and configure their profile.</p>
            </div>
            <button className="min-h-11 rounded-full border border-line px-4" onClick={requestClose} type="button">Close</button>
          </div>
        )}
        onCancel={requestClose}
        title="Add New Coach"
      >
        <CoachStepIndicator includeCompensation={canEditCompensation} step={step} />
        <div className="mt-4 grid gap-4">
          {step === 1 ? <PersonalStep draft={draft} onPhoto={(file, error) => { setPhoto(file); setPhotoError(error); }} photo={photo} photoError={photoError} update={update} /> : null}
          {step === 2 ? <ProfessionalStep draft={draft} update={update} /> : null}
          {step === 3 ? <EmploymentStep branches={activeBranches} draft={draft} managers={managerRows} update={update} /> : null}
          {step === 4 && canEditCompensation ? <CompensationStep draft={draft} update={update} /> : null}
          {step === 5 ? <AccessReviewStep candidate={candidate} draft={draft} seatChoices={seatChoices} seatRequired update={update} /> : null}
          {message ? <p role="alert">{message}</p> : null}
          {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
          <div className="mt-2 flex flex-wrap justify-between gap-3">
            <button className="min-h-11 rounded-full border border-line px-4" onClick={requestClose} type="button">Cancel</button>
            <div className="flex flex-wrap gap-2">
              {step > 1 ? (
                <button className="min-h-11 rounded-full border border-line px-4" onClick={goPrevious} type="button">Previous</button>
              ) : null}
              {step < 5 ? (
                <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => void goNext()} type="button">
                  Next: {nextLabel()}
                </button>
              ) : (
                <button
                  className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60"
                  disabled={create.isPending || candidate?.alreadyInWorkspace === true}
                  onClick={() => {
                    const error = stepError(5, draft, { requireSeat: true });
                    if (error) {
                      setMessage(error);
                      return;
                    }
                    create.mutate();
                  }}
                  type="button"
                >
                  {create.isPending ? "Creating..." : "Create coach"}
                </button>
              )}
            </div>
          </div>
        </div>
      </FormDialog>
      {confirmCancel ? (
        <ConfirmDialog
          body="Discard this coach draft? Unsaved changes will be lost."
          confirmLabel="Discard"
          onCancel={() => setConfirmCancel(false)}
          onConfirm={onCancel}
          pending={false}
          title="Discard changes"
        />
      ) : null}
    </>
  );
}
