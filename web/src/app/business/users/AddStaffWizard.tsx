"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useMemo, useState, useSyncExternalStore } from "react";
import { FormDialog } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import type { AccessRoleList } from "../../../lib/accessRoles";
import { getSession, subscribeSession } from "../../../lib/session";
import type { CreatedWorkspaceUser, Page, WorkspacePlan, WorkspaceUser } from "../../../lib/types";
import { uploadStaffPhoto } from "./StaffPhotoField";
import { AccessStep, EmploymentStep, PersonalStep, ReviewStep, StepIndicator, type BranchOption, type Candidate } from "./StaffWizardSteps";
import { staffRoleLabel, useStaffRoles } from "./UserDialogs";
import {
  emptyStaffDraft,
  roleKind,
  splitFullName,
  stepError,
  type StaffDraft,
} from "./staffWizard";

export type StaffWizardResult = {
  user: CreatedWorkspaceUser & { staffProfileId?: string | null; invitationToken?: string | null; status?: string; employeeCode?: string | null };
  photoWarning?: string;
  compensationHref?: string;
};

export function StaffWizard({ onCancel, onCreated }: { onCancel: () => void; onCreated: (result: StaffWizardResult) => void }) {
  const today = new Date().toISOString().slice(0, 10);
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  const { roles, loading: rolesLoading, failed: rolesFailed } = useStaffRoles();
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<StaffDraft>(() => emptyStaffDraft(today));
  const [photo, setPhoto] = useState<File | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [candidate, setCandidate] = useState<Candidate | null>(null);
  const branches = useQuery({ queryKey: ["branches", "staff-wizard"], queryFn: () => api<Page<BranchOption>>("/branches?page=1&pageSize=50") });
  const plans = useQuery({ queryKey: ["workspace-plans", "staff-wizard"], queryFn: () => api<WorkspacePlan[]>("/business/plans") });
  const managers = useQuery({ queryKey: ["workspace-users", "managers", tenantId], enabled: tenantId !== null, queryFn: () => api<Page<WorkspaceUser>>("/business/users?page=1&pageSize=100") });
  const access = useQuery({ queryKey: ["access-roles", tenantId], enabled: tenantId !== null, queryFn: () => api<AccessRoleList>("/access-roles") });
  const activeBranches = (branches.data?.items ?? []).filter((branch) => !branch.status || branch.status === "Active");
  const seatChoices = (plans.data ?? []).filter((plan) => plan.usable && plan.available > 0);
  const selectedRole = useMemo(() => (access.data?.roles ?? []).find((role) => role.name === draft.roleName) ?? null, [access.data, draft.roleName]);
  const kind = roleKind(draft.roleName);
  const canOverride = access.data?.canManage === true;

  const create = useMutation({
    mutationFn: async (mode: "draft" | "invite") => {
      const name = splitFullName(draft.fullName);
      if (!name) {
        throw new ApiError("Enter a first and last name.", 400);
      }
      const body = {
        email: draft.email.trim().toLowerCase(),
        firstName: name.firstName,
        lastName: name.lastName,
        phone: draft.phone.trim(),
        roleName: draft.roleName,
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
        subscriptionId: mode === "draft" ? null : draft.subscriptionId || null,
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
        linkExistingUser: draft.linkExistingUser,
        saveAsDraft: mode === "draft",
        sendInvitation: mode === "invite",
      };
      const user = await api<StaffWizardResult["user"]>("/business/users", { method: "POST", body: JSON.stringify(body) });
      if (photo) {
        try {
          await uploadStaffPhoto(user.id, photo);
        } catch (error) {
          return { user, photoWarning: error instanceof ApiError ? error.message : "The staff member was created, but the photo was not.", compensationHref: user.staffProfileId ? `/business/payroll?staff=${user.staffProfileId}` : undefined };
        }
      }
      return { user, compensationHref: user.staffProfileId ? `/business/payroll?staff=${user.staffProfileId}` : undefined };
    },
    onSuccess: (result) => onCreated(result),
  });

  function update(patch: Partial<StaffDraft>) {
    setMessage(null);
    setDraft((current) => ({ ...current, ...patch }));
  }

  async function lookupIdentity() {
    setMessage(null);
    try {
      const match = await api<Candidate | null>(`/business/users/lookup?email=${encodeURIComponent(draft.email.trim())}&phone=${encodeURIComponent(draft.phone.trim())}`);
      setCandidate(match);
      if (match?.alreadyInWorkspace) {
        setMessage("That person already has access to this workspace.");
        return;
      }
      if (match) {
        update({
          linkExistingUser: true,
          fullName: `${match.firstName} ${match.lastName}`.trim(),
          email: match.email,
          phone: match.phone ?? draft.phone,
        });
      } else {
        update({ linkExistingUser: false });
      }
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : "Could not look up that person.");
    }
  }

  function next() {
    const error = stepError(step, draft, activeBranches.length > 0);
    if (error) {
      setMessage(error);
      return;
    }
    if (step === 1) {
      void lookupIdentity().then(() => setStep(2));
      return;
    }
    setMessage(null);
    setStep((current) => current + 1);
  }

  return (
    <FormDialog title={`Add staff · Step ${step} of 4`} onCancel={onCancel} wide>
      <div className="mt-4 grid gap-3">
        <StepIndicator step={step} />
        {step === 1 ? (
          <PersonalStep draft={draft} photoError={photoError} update={update} onPhoto={(file, error) => { setPhoto(file); setPhotoError(error); }} />
        ) : null}
        {step === 2 ? (
          <EmploymentStep
            branches={activeBranches}
            draft={draft}
            kind={kind}
            managers={(managers.data?.items ?? []).filter((row) => row.role !== "Trainer" && row.id)}
            update={update}
          />
        ) : null}
        {step === 3 ? (
          <AccessStep
            canOverride={canOverride}
            draft={draft}
            failed={rolesFailed}
            kind={kind}
            loading={rolesLoading}
            roles={roles}
            seatChoices={seatChoices}
            selectedRole={selectedRole}
            update={update}
          />
        ) : null}
        {step === 4 ? <ReviewStep candidate={candidate} draft={draft} kind={kind} mode="create" roleLabel={staffRoleLabel(draft.roleName)} selectedRole={selectedRole} seats={seatChoices} /> : null}
        {candidate && !candidate.alreadyInWorkspace && draft.linkExistingUser ? (
          <p className="rounded-xl border border-line px-4 py-3 text-sm">Existing LifeFit AI account found for {candidate.email}. Creating staff will link that account.</p>
        ) : null}
        {message ? <p role="alert">{message}</p> : null}
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
          <button className="min-h-11 rounded-full border border-line px-4" disabled={create.isPending} onClick={onCancel} type="button">Cancel</button>
          <div className="flex flex-wrap gap-3">
            {step > 1 ? <button className="min-h-11 rounded-full px-4" disabled={create.isPending} onClick={() => setStep((current) => current - 1)} type="button">Previous</button> : null}
            {step < 4 ? (
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={next} type="button">Next</button>
            ) : (
              <>
                <button className="min-h-11 rounded-full border border-line px-4 disabled:opacity-60" disabled={create.isPending} onClick={() => create.mutate("draft")} type="button">{create.isPending ? "Saving..." : "Save as draft"}</button>
                <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={create.isPending || Boolean(photoError)} onClick={() => { const error = stepError(3, draft, activeBranches.length > 0) ?? stepError(2, draft, activeBranches.length > 0) ?? stepError(1, draft, false); if (error) { setMessage(error); return; } create.mutate("invite"); }} type="button">{create.isPending ? "Sending..." : "Create staff & send invitation"}</button>
              </>
            )}
          </div>
        </div>
      </div>
    </FormDialog>
  );
}
