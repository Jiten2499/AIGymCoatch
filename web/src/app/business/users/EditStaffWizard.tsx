"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { FormDialog } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import type { AccessRoleList } from "../../../lib/accessRoles";
import { getSession, subscribeSession } from "../../../lib/session";
import type { Page, WorkspacePlan, WorkspaceUser } from "../../../lib/types";
import { uploadStaffPhoto } from "./StaffPhotoField";
import { AccessStep, EmploymentStep, PersonalStep, ReviewStep, StepIndicator, type BranchOption } from "./StaffWizardSteps";
import { staffRoleLabel, useStaffRoles } from "./UserDialogs";
import {
  draftFromDetail,
  emptyStaffDraft,
  roleKind,
  splitFullName,
  stepError,
  type StaffDetail,
  type StaffDraft,
} from "./staffWizard";

export function EditStaffWizard({
  userId,
  onCancel,
  onSaved,
}: {
  userId: string;
  onCancel: () => void;
  onSaved: (result?: { photoWarning?: string; compensationHref?: string }) => void;
}) {
  const today = new Date().toISOString().slice(0, 10);
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  const detail = useQuery({
    queryKey: ["staff-detail", tenantId, userId],
    enabled: tenantId !== null,
    queryFn: () => api<StaffDetail>(`/business/users/${userId}`),
  });
  const { roles, loading: rolesLoading, failed: rolesFailed } = useStaffRoles(detail.data?.roleName);
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<StaffDraft>(() => emptyStaffDraft(today));
  const [hydrated, setHydrated] = useState(false);
  const [photo, setPhoto] = useState<File | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const branches = useQuery({ queryKey: ["branches", "staff-wizard"], queryFn: () => api<Page<BranchOption>>("/branches?page=1&pageSize=50") });
  const plans = useQuery({ queryKey: ["workspace-plans", "staff-wizard"], queryFn: () => api<WorkspacePlan[]>("/business/plans") });
  const managers = useQuery({ queryKey: ["workspace-users", "managers", tenantId], enabled: tenantId !== null, queryFn: () => api<Page<WorkspaceUser>>("/business/users?page=1&pageSize=100") });
  const access = useQuery({ queryKey: ["access-roles", tenantId], enabled: tenantId !== null, queryFn: () => api<AccessRoleList>("/access-roles") });

  useEffect(() => {
    if (detail.data && !hydrated) {
      setDraft(draftFromDetail(detail.data));
      setHydrated(true);
    }
  }, [detail.data, hydrated]);

  const activeBranches = (branches.data?.items ?? []).filter((branch) => !branch.status || branch.status === "Active");
  const seatChoices = useMemo(() => {
    const usable = (plans.data ?? []).filter((plan) => plan.usable && plan.available > 0);
    const currentId = detail.data?.subscriptionId;
    if (!currentId || usable.some((plan) => plan.subscriptionId === currentId)) {
      return usable;
    }
    const current = (plans.data ?? []).find((plan) => plan.subscriptionId === currentId);
    return current ? [current, ...usable] : usable;
  }, [plans.data, detail.data?.subscriptionId]);
  const selectedRole = useMemo(() => (access.data?.roles ?? []).find((role) => role.name === draft.roleName) ?? null, [access.data, draft.roleName]);
  const kind = roleKind(draft.roleName);
  const canOverride = access.data?.canManage === true;
  const isOwner = detail.data?.isOwner === true;

  const save = useMutation({
    mutationFn: async () => {
      const name = splitFullName(draft.fullName);
      if (!name) {
        throw new ApiError("Enter a first and last name.", 400);
      }
      const deactivated = draft.employmentStatus === "Deactivated" || draft.employmentStatus === "Inactive";
      const body = {
        firstName: name.firstName,
        lastName: name.lastName,
        status: deactivated ? "Disabled" : "Active",
        roleName: isOwner ? "GymOwner" : draft.roleName,
        phone: draft.phone.trim(),
        dateOfBirth: draft.dateOfBirth || null,
        gender: draft.gender || null,
        address: draft.address.trim() || null,
        emergencyContactName: draft.emergencyName.trim() || null,
        emergencyContactPhone: draft.emergencyPhone.trim() || null,
        employeeCode: draft.employeeCode.trim() || null,
        designation: draft.designation.trim(),
        department: draft.department.trim() || null,
        employmentType: draft.employmentType || null,
        joiningDate: draft.joiningDate || null,
        branchId: draft.branchId || null,
        reportingManagerUserId: draft.reportingManagerUserId || null,
        employmentNotes: draft.employmentNotes.trim() || null,
        jobDescription: kind === "other" ? draft.jobDescription.trim() || null : null,
        authorizedBranchIds: kind === "manager" ? draft.authorizedBranchIds : draft.branchId ? [draft.branchId] : [],
        subscriptionId: draft.subscriptionId || null,
        scopeOverrides: canOverride
          ? [
              { resource: "members", scope: draft.memberScope },
              ...(kind === "accountant" ? [{ resource: "payments", scope: draft.paymentScope }] : []),
            ]
          : null,
        permissionOverrides: canOverride ? draft.permissionOverrides : null,
        specialization: kind === "trainer" ? draft.specialization.trim() || null : null,
        certification: kind === "trainer" ? draft.certification.trim() || null : null,
        experienceYears: kind === "trainer" && draft.experienceYears.trim() ? Number(draft.experienceYears) : null,
        trainingExpertise: kind === "trainer" ? draft.trainingExpertise.trim() || null : null,
        employmentStatus: draft.employmentStatus,
      };
      await api(`/business/users/${userId}`, { method: "PUT", body: JSON.stringify(body) });
      if (photo) {
        try {
          await uploadStaffPhoto(userId, photo);
        } catch (error) {
          return {
            photoWarning: error instanceof ApiError ? error.message : "Staff was updated, but the photo was not.",
            compensationHref: detail.data?.staffProfileId ? `/business/payroll?staff=${detail.data.staffProfileId}` : undefined,
          };
        }
      }
      return { compensationHref: detail.data?.staffProfileId ? `/business/payroll?staff=${detail.data.staffProfileId}` : undefined };
    },
    onSuccess: (result) => onSaved(result),
  });

  function update(patch: Partial<StaffDraft>) {
    setMessage(null);
    setDraft((current) => ({ ...current, ...patch }));
  }

  function next() {
    const error = stepError(step, draft, activeBranches.length > 0, { requireSeat: false });
    if (error) {
      setMessage(error);
      return;
    }
    setMessage(null);
    setStep((current) => current + 1);
  }

  if (detail.isLoading || !hydrated) {
    return (
      <FormDialog title="Edit staff" onCancel={onCancel} wide>
        <p className="mt-4">Loading staff details…</p>
      </FormDialog>
    );
  }

  if (detail.error instanceof ApiError) {
    return (
      <FormDialog title="Edit staff" onCancel={onCancel} wide>
        <p className="mt-4" role="alert">{detail.error.message}</p>
      </FormDialog>
    );
  }

  return (
    <FormDialog title={`Edit staff · Step ${step} of 4`} onCancel={onCancel} wide>
      <div className="mt-4 grid gap-3">
        <StepIndicator step={step} />
        {step === 1 ? (
          <PersonalStep
            draft={draft}
            emailLocked
            photoError={photoError}
            update={update}
            onPhoto={(file, error) => { setPhoto(file); setPhotoError(error); }}
          />
        ) : null}
        {step === 2 ? (
          <EmploymentStep
            branches={activeBranches}
            draft={draft}
            kind={kind}
            managers={(managers.data?.items ?? []).filter((row) => row.role !== "Trainer" && row.id !== userId)}
            showStatus
            statusLocked={isOwner}
            update={update}
          />
        ) : null}
        {step === 3 ? (
          <AccessStep
            canOverride={canOverride && !isOwner}
            currentPlanName={detail.data?.planName}
            draft={draft}
            failed={rolesFailed}
            kind={kind}
            loading={rolesLoading}
            roleLocked={isOwner}
            roles={roles}
            seatChoices={seatChoices}
            seatRequired={false}
            selectedRole={selectedRole}
            update={update}
          />
        ) : null}
        {step === 4 ? (
          <ReviewStep
            currentPlanName={detail.data?.planName}
            draft={draft}
            kind={kind}
            mode="edit"
            roleLabel={staffRoleLabel(draft.roleName)}
            selectedRole={selectedRole}
            seats={seatChoices}
          />
        ) : null}
        {message ? <p role="alert">{message}</p> : null}
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
          <button className="min-h-11 rounded-full border border-line px-4" disabled={save.isPending} onClick={onCancel} type="button">Cancel</button>
          <div className="flex flex-wrap gap-3">
            {step > 1 ? <button className="min-h-11 rounded-full px-4" disabled={save.isPending} onClick={() => setStep((current) => current - 1)} type="button">Previous</button> : null}
            {step < 4 ? (
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={next} type="button">Next</button>
            ) : (
              <button
                className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60"
                disabled={save.isPending || Boolean(photoError)}
                onClick={() => {
                  const error = stepError(3, draft, activeBranches.length > 0, { requireSeat: false })
                    ?? stepError(2, draft, activeBranches.length > 0, { requireSeat: false })
                    ?? stepError(1, draft, false, { requireSeat: false });
                  if (error) {
                    setMessage(error);
                    return;
                  }
                  save.mutate();
                }}
                type="button"
              >
                {save.isPending ? "Saving..." : "Save changes"}
              </button>
            )}
          </div>
        </div>
      </div>
    </FormDialog>
  );
}
