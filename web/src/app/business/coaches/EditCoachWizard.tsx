"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { FormDialog } from "../../../components/FormDialog";
import { api, apiBlob, ApiError } from "../../../lib/api";
import { listCompensation, saveCompensation } from "../../../lib/payroll";
import { getSession, subscribeSession } from "../../../lib/session";
import type { Coach, Page, WorkspacePlan, WorkspaceUser } from "../../../lib/types";
import {
  AccessReviewStep,
  CompensationStep,
  CoachStepIndicator,
  EmploymentStep,
  PersonalStep,
  ProfessionalStep,
  type BranchOption,
} from "./AddCoachWizardSteps";
import { coachPhotoError, uploadCoachPhoto } from "./CoachPhotoField";
import {
  certificationText,
  commissionEligibleFor,
  employmentNotes,
  emptyCoachDraft,
  needsMonthlySalary,
  needsPerSessionRate,
  resolvedFirstName,
  specializationText,
  stepError,
  type CoachDraft,
} from "./coachWizard";
import { draftFromCoachDetail, findCoachPay, type CoachDetail } from "./coachWizardDetail";

const wizardSteps = ["Personal", "Professional", "Employment", "Compensation", "Access"] as const;

export function EditCoachWizard({
  coach,
  onCancel,
  onSaved,
  canEditCompensation,
}: {
  coach: Coach;
  onCancel: () => void;
  onSaved: (result?: { photoWarning?: string; compensationWarning?: string }) => void;
  canEditCompensation: boolean;
}) {
  const today = new Date().toISOString().slice(0, 10);
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  const detail = useQuery({
    queryKey: ["coach-edit", tenantId, coach.id],
    enabled: tenantId !== null,
    queryFn: () => api<CoachDetail>(`/trainers/${coach.id}`),
  });
  const compensation = useQuery({
    queryKey: ["payroll-compensation", tenantId],
    enabled: tenantId !== null && canEditCompensation,
    queryFn: () => listCompensation(),
  });
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<CoachDraft>(() => emptyCoachDraft(today));
  const [hydrated, setHydrated] = useState(false);
  const [photo, setPhoto] = useState<File | null>(null);
  const [removePhoto, setRemovePhoto] = useState(false);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [baseline, setBaseline] = useState<string>("");
  const branches = useQuery({ queryKey: ["branches", "coach-wizard"], queryFn: () => api<Page<BranchOption>>("/branches?page=1&pageSize=100") });
  const plans = useQuery({ queryKey: ["workspace-plans", "coach-wizard"], queryFn: () => api<WorkspacePlan[]>("/business/plans") });
  const managers = useQuery({
    queryKey: ["workspace-users", "coach-managers", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<Page<WorkspaceUser>>("/business/users?page=1&pageSize=100"),
  });
  const existingPhoto = useQuery({
    queryKey: ["coach-photo", coach.id],
    enabled: (detail.data?.hasPhoto ?? coach.hasPhoto) === true && !photo && !removePhoto,
    retry: false,
    queryFn: () => apiBlob(`/trainers/${coach.id}/profile-photo`),
  });

  useEffect(() => {
    if (!detail.data || hydrated) {
      return;
    }
    const next = draftFromCoachDetail(detail.data, today);
    const pay = findCoachPay(compensation.data, detail.data.staffProfileId);
    if (pay) {
      next.monthlySalary = String(pay.baseSalary || "");
      if (!next.perSessionRate && pay.allowances) {
        next.perSessionRate = String(pay.allowances);
      }
      if (pay.baseSalary && next.commissionEligible) {
        next.salaryType = "FixedPlusCommission";
      } else if (pay.baseSalary) {
        next.salaryType = "FixedMonthly";
      }
    }
    setDraft(next);
    setBaseline(JSON.stringify(next));
    setHydrated(true);
  }, [detail.data, compensation.data, hydrated, today]);

  const activeBranches = (branches.data?.items ?? []).filter((branch) => !branch.status || branch.status === "Active");
  const managerRows = (managers.data?.items ?? []).filter((row) => row.role !== "Trainer");
  const seatChoices = useMemo(() => {
    const usable = (plans.data ?? []).filter((plan) => plan.usable && plan.available > 0);
    const currentId = detail.data?.subscriptionId;
    if (!currentId || usable.some((plan) => plan.subscriptionId === currentId)) {
      return usable;
    }
    const current = (plans.data ?? []).find((plan) => plan.subscriptionId === currentId);
    return current ? [current, ...usable] : usable;
  }, [plans.data, detail.data?.subscriptionId]);

  const save = useMutation({
    mutationFn: async () => {
      const picture = coachPhotoError(photo);
      if (picture) {
        throw new ApiError(picture, 400);
      }
      const staffProfileId = detail.data?.staffProfileId ?? coach.staffProfileId ?? null;
      await api(`/trainers/${coach.id}`, {
        method: "PUT",
        body: JSON.stringify({
          firstName: resolvedFirstName(draft),
          lastName: draft.lastName.trim(),
          phone: draft.phone.trim(),
          setPhone: true,
          bio: draft.bio.trim() || null,
          specialization: specializationText(draft) || null,
          experienceYears: draft.experienceYears.trim() ? Number(draft.experienceYears) : null,
          certification: certificationText(draft) || null,
          profileImageUrl: coach.profileImageUrl,
          employeeCode: draft.employeeCode.trim() || detail.data?.employeeCode || coach.employeeCode || null,
          joiningDate: draft.joiningDate || null,
          commissionEligible: draft.commissionEligible || commissionEligibleFor(draft.salaryType),
          setEmployment: true,
          branchId: draft.branchId || null,
          status: draft.employmentStatus === "Inactive" ? "Inactive" : "Active",
          dateOfBirth: draft.dateOfBirth || null,
          gender: draft.gender || null,
          address: draft.address.trim() || null,
          emergencyContactName: draft.emergencyName.trim() || null,
          emergencyContactPhone: draft.emergencyPhone.trim() || null,
          designation: draft.designation.trim(),
          employmentType: draft.employmentType || null,
          reportingManagerUserId: draft.reportingManagerUserId || null,
          employmentNotes: employmentNotes(draft) || null,
          setStaffProfile: true,
          subscriptionId: draft.subscriptionId || null,
        }),
      });

      let photoWarning: string | undefined;
      let compensationWarning: string | undefined;
      if (photo) {
        try {
          await uploadCoachPhoto(coach.id, photo);
        } catch (error) {
          photoWarning = error instanceof ApiError ? error.message : "The coach was saved, but the photo was not.";
        }
      } else if (removePhoto && (detail.data?.hasPhoto || coach.hasPhoto)) {
        try {
          await api(`/trainers/${coach.id}/profile-photo`, { method: "DELETE" });
        } catch (error) {
          photoWarning = error instanceof ApiError ? error.message : "The coach was saved, but the photo was not removed.";
        }
      }

      if (canEditCompensation && staffProfileId) {
        const baseSalary = needsMonthlySalary(draft.salaryType) && draft.monthlySalary.trim() ? Number(draft.monthlySalary) : 0;
        const allowances = needsPerSessionRate(draft.salaryType) && draft.perSessionRate.trim() ? Number(draft.perSessionRate) : 0;
        if ((baseSalary > 0 || allowances > 0 || employmentNotes(draft)) && !Number.isNaN(baseSalary) && !Number.isNaN(allowances)) {
          try {
            await saveCompensation(staffProfileId, baseSalary, allowances, draft.joiningDate || today, employmentNotes(draft) || null);
          } catch (error) {
            compensationWarning = error instanceof ApiError ? error.message : "The coach was saved, but compensation was not updated.";
          }
        }
      }

      return { photoWarning, compensationWarning };
    },
    onSuccess: (result) => onSaved(result),
  });

  function update(patch: Partial<CoachDraft>) {
    setMessage(null);
    setDraft((current) => ({ ...current, ...patch }));
  }

  function goNext() {
    const error = stepError(step, draft, { requireCompensation: canEditCompensation && step === 4 });
    if (error) {
      setMessage(error);
      return;
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
    if (JSON.stringify(draft) !== baseline || photo || removePhoto) {
      setConfirmCancel(true);
      return;
    }
    onCancel();
  }

  if (detail.isLoading || !hydrated) {
    return <FormDialog onCancel={onCancel} title="Edit coach"><p className="text-muted">Loading coach profile...</p></FormDialog>;
  }
  if (detail.error instanceof ApiError) {
    return <FormDialog onCancel={onCancel} title="Edit coach"><p role="alert">{detail.error.message}</p></FormDialog>;
  }

  return (
    <>
      <FormDialog
        large
        header={(
          <div className="flex items-start justify-between gap-4">
            <div>
              <h2 className="text-xl font-semibold" id="form-dialog-title">Edit Coach</h2>
              <p className="mt-1 text-sm text-muted">Update profile, employment, seat, and access details.</p>
            </div>
            <button className="min-h-11 rounded-full border border-line px-4" onClick={requestClose} type="button">Close</button>
          </div>
        )}
        onCancel={requestClose}
        title="Edit Coach"
      >
        <CoachStepIndicator includeCompensation={canEditCompensation} step={step} />
        <div className="mt-4 grid gap-4">
          {step === 1 ? (
            <PersonalStep
              draft={draft}
              emailLocked
              existingPhoto={removePhoto ? null : existingPhoto.data ?? null}
              onPhoto={(file, error) => { setRemovePhoto(false); setPhoto(file); setPhotoError(error); }}
              onRemovePhoto={() => { setPhoto(null); setRemovePhoto(true); setPhotoError(null); }}
              photo={photo}
              photoError={photoError}
              update={update}
            />
          ) : null}
          {step === 2 ? <ProfessionalStep draft={draft} update={update} /> : null}
          {step === 3 ? <EmploymentStep branches={activeBranches} draft={draft} managers={managerRows} update={update} /> : null}
          {step === 4 && canEditCompensation ? <CompensationStep draft={draft} update={update} /> : null}
          {step === 5 ? <AccessReviewStep candidate={null} draft={draft} mode="edit" seatChoices={seatChoices} update={update} /> : null}
          {message ? <p role="alert">{message}</p> : null}
          {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
          <div className="mt-2 flex flex-wrap justify-between gap-3">
            <button className="min-h-11 rounded-full border border-line px-4" onClick={requestClose} type="button">Cancel</button>
            <div className="flex flex-wrap gap-2">
              {step > 1 ? <button className="min-h-11 rounded-full border border-line px-4" onClick={goPrevious} type="button">Previous</button> : null}
              {step < 5 ? (
                <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={goNext} type="button">Next: {nextLabel()}</button>
              ) : (
                <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={save.isPending} onClick={() => save.mutate()} type="button">
                  {save.isPending ? "Saving..." : "Save coach"}
                </button>
              )}
            </div>
          </div>
        </div>
      </FormDialog>
      {confirmCancel ? (
        <ConfirmDialog
          body="Discard edits to this coach? Unsaved changes will be lost."
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
