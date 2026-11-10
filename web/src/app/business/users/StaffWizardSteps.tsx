"use client";

import { FormField } from "../../../components/FormDialog";
import type { AccessRoleList } from "../../../lib/accessRoles";
import type { WorkspacePlan, WorkspaceUser } from "../../../lib/types";
import { StaffPhotoField } from "./StaffPhotoField";
import { staffRoleLabel, type StaffRoleOption } from "./UserDialogs";
import {
  dataScopes,
  employmentTypeLabel,
  employmentTypes,
  genders,
  scopeLabel,
  splitFullName,
  type StaffDraft,
} from "./staffWizard";

export type BranchOption = { id: string; name: string; status?: string };
export type Candidate = { userId: string; email: string; firstName: string; lastName: string; phone: string | null; alreadyInWorkspace: boolean };

export const employmentStatuses = ["Active", "Suspended", "Draft", "PendingInvitation", "Deactivated"] as const;

export function StepIndicator({ step }: { step: number }) {
  const labels = ["Personal", "Employment", "Access", "Review"];
  return (
    <ol className="mb-2 grid grid-cols-4 gap-2 text-sm">
      {labels.map((label, index) => {
        const current = index + 1;
        return (
          <li className={`rounded-full border px-3 py-2 text-center ${current === step ? "border-accent bg-accent/10 font-semibold" : "border-line text-muted"}`} key={label}>
            {current}. {label}
          </li>
        );
      })}
    </ol>
  );
}

export function PersonalStep({
  draft,
  photoError,
  emailLocked,
  update,
  onPhoto,
}: {
  draft: StaffDraft;
  photoError: string | null;
  emailLocked?: boolean;
  update: (patch: Partial<StaffDraft>) => void;
  onPhoto: (file: File | null, error: string | null) => void;
}) {
  const name = splitFullName(draft.fullName);
  return (
    <>
      <StaffPhotoField firstName={name?.firstName ?? ""} lastName={name?.lastName ?? ""} onFile={onPhoto} />
      {photoError ? <p role="alert">{photoError}</p> : null}
      <FormField label="Full name"><input className="field" onChange={(event) => update({ fullName: event.target.value })} value={draft.fullName} /></FormField>
      <FormField label="Email address">
        <input
          className="field"
          disabled={emailLocked}
          onChange={(event) => update({ email: event.target.value, linkExistingUser: false })}
          type="email"
          value={draft.email}
        />
      </FormField>
      <FormField label="Mobile number"><input className="field" onChange={(event) => update({ phone: event.target.value, linkExistingUser: false })} value={draft.phone} /></FormField>
      <div className="grid gap-3 md:grid-cols-2">
        <FormField label="Date of birth"><input className="field" onChange={(event) => update({ dateOfBirth: event.target.value })} type="date" value={draft.dateOfBirth} /></FormField>
        <FormField label="Gender">
          <select aria-label="Gender" className="field" onChange={(event) => update({ gender: event.target.value })} value={draft.gender}>
            {genders.map((item) => <option key={item || "none"} value={item}>{item || "Prefer not to say"}</option>)}
          </select>
        </FormField>
      </div>
      <FormField label="Address"><input className="field" onChange={(event) => update({ address: event.target.value })} value={draft.address} /></FormField>
      <div className="grid gap-3 md:grid-cols-2">
        <FormField label="Emergency contact name"><input className="field" onChange={(event) => update({ emergencyName: event.target.value })} value={draft.emergencyName} /></FormField>
        <FormField label="Emergency contact number"><input className="field" onChange={(event) => update({ emergencyPhone: event.target.value })} value={draft.emergencyPhone} /></FormField>
      </div>
    </>
  );
}

export function EmploymentStep({
  draft,
  branches,
  managers,
  kind,
  showStatus,
  statusLocked,
  update,
}: {
  draft: StaffDraft;
  branches: BranchOption[];
  managers: WorkspaceUser[];
  kind: string;
  showStatus?: boolean;
  statusLocked?: boolean;
  update: (patch: Partial<StaffDraft>) => void;
}) {
  return (
    <>
      <FormField label="Employee ID"><input className="field" onChange={(event) => update({ employeeCode: event.target.value })} placeholder="Auto-generated if left blank" value={draft.employeeCode} /></FormField>
      <FormField label="Designation / job title"><input className="field" onChange={(event) => update({ designation: event.target.value })} value={draft.designation} /></FormField>
      <div className="grid gap-3 md:grid-cols-2">
        <FormField label="Department"><input className="field" onChange={(event) => update({ department: event.target.value })} value={draft.department} /></FormField>
        <FormField label="Employment type">
          <select aria-label="Employment type" className="field" onChange={(event) => update({ employmentType: event.target.value })} value={draft.employmentType}>
            {employmentTypes.map((item) => <option key={item} value={item}>{employmentTypeLabel(item)}</option>)}
          </select>
        </FormField>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <FormField label="Joining date"><input className="field" onChange={(event) => update({ joiningDate: event.target.value })} type="date" value={draft.joiningDate} /></FormField>
        <FormField label="Branch / location">
          <select aria-label="Branch" className="field" onChange={(event) => update({ branchId: event.target.value })} value={draft.branchId}>
            <option value="">{branches.length ? "Select branch" : "No branches configured"}</option>
            {branches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
          </select>
        </FormField>
      </div>
      {showStatus ? (
        <FormField label="Employment status">
          <select aria-label="Employment status" className="field" disabled={statusLocked} onChange={(event) => update({ employmentStatus: event.target.value })} value={draft.employmentStatus}>
            {employmentStatuses.map((item) => <option key={item} value={item}>{item}</option>)}
          </select>
        </FormField>
      ) : null}
      <FormField label="Reporting manager">
        <select aria-label="Reporting manager" className="field" onChange={(event) => update({ reportingManagerUserId: event.target.value })} value={draft.reportingManagerUserId}>
          <option value="">None</option>
          {managers.map((row) => <option key={row.id} value={row.id}>{row.firstName} {row.lastName} · {staffRoleLabel(row.role)}</option>)}
        </select>
      </FormField>
      <FormField label="Employment notes"><textarea className="field min-h-24" onChange={(event) => update({ employmentNotes: event.target.value })} value={draft.employmentNotes} /></FormField>
      {kind === "trainer" ? (
        <>
          <FormField label="Specialization"><input className="field" onChange={(event) => update({ specialization: event.target.value })} value={draft.specialization} /></FormField>
          <FormField label="Certifications"><input className="field" onChange={(event) => update({ certification: event.target.value })} value={draft.certification} /></FormField>
          <FormField label="Experience (years)"><input className="field" min={0} onChange={(event) => update({ experienceYears: event.target.value })} type="number" value={draft.experienceYears} /></FormField>
          <FormField label="Training expertise"><textarea className="field min-h-24" onChange={(event) => update({ trainingExpertise: event.target.value })} value={draft.trainingExpertise} /></FormField>
        </>
      ) : null}
      {kind === "manager" ? (
        <FormField label="Managed branches">
          <div className="grid gap-2 rounded-xl border border-line p-3">
            {branches.map((branch) => {
              const checked = draft.authorizedBranchIds.includes(branch.id);
              return (
                <label className="flex items-center gap-2" key={branch.id}>
                  <input checked={checked} onChange={() => update({ authorizedBranchIds: checked ? draft.authorizedBranchIds.filter((id) => id !== branch.id) : [...draft.authorizedBranchIds, branch.id] })} type="checkbox" />
                  {branch.name}
                </label>
              );
            })}
          </div>
        </FormField>
      ) : null}
      {kind === "other" ? <FormField label="Job description"><textarea className="field min-h-24" onChange={(event) => update({ jobDescription: event.target.value })} value={draft.jobDescription} /></FormField> : null}
    </>
  );
}

export function AccessStep({
  draft,
  roles,
  loading,
  failed,
  seatChoices,
  selectedRole,
  canOverride,
  kind,
  roleLocked,
  seatRequired,
  currentPlanName,
  update,
}: {
  draft: StaffDraft;
  roles: StaffRoleOption[];
  loading: boolean;
  failed: boolean;
  seatChoices: WorkspacePlan[];
  selectedRole: AccessRoleList["roles"][number] | null;
  canOverride: boolean;
  kind: string;
  roleLocked?: boolean;
  seatRequired?: boolean;
  currentPlanName?: string | null;
  update: (patch: Partial<StaffDraft>) => void;
}) {
  return (
    <>
      <p className="text-sm text-muted">Seat allocation is separate from role permissions. Changing role does not consume an extra seat when the current seat is kept.</p>
      <FormField label="Business seat">
        <select aria-label="Business seat" className="field" onChange={(event) => update({ subscriptionId: event.target.value })} value={draft.subscriptionId}>
          <option value="">{seatRequired === false ? "Keep current seat / none" : seatChoices.length ? "Select a seat plan" : "No seats available"}</option>
          {seatChoices.map((plan) => <option key={plan.subscriptionId} value={plan.subscriptionId}>{plan.planName} · {plan.available} seats available</option>)}
        </select>
      </FormField>
      {currentPlanName && !draft.subscriptionId ? <p className="text-sm text-muted">Current plan: {currentPlanName}</p> : null}
      <FormField label="Role">
        {loading ? <p>Loading roles…</p> : null}
        {failed ? <p role="alert">Custom roles could not be loaded. The standard roles are shown below.</p> : null}
        <select aria-label="Role" className="field" disabled={roleLocked} onChange={(event) => update({ roleName: event.target.value })} value={draft.roleName}>
          {roles.map((role) => <option key={role.name} value={role.name}>{role.custom ? `${role.label} (custom)` : role.label}</option>)}
        </select>
      </FormField>
      {selectedRole ? (
        <div className="rounded-xl border border-line p-4">
          <p className="font-semibold">Permission preview</p>
          <p className="mt-1 text-sm text-muted">{selectedRole.permissionSummary || "No permissions listed for this role."}</p>
          <ul className="mt-3 grid gap-1 text-sm text-muted">
            {selectedRole.scopes.map((scope) => <li key={scope.resource}>{scope.resource}: {scopeLabel(scope.scope)}</li>)}
          </ul>
        </div>
      ) : null}
      {canOverride ? (
        <>
          <FormField label="Members data access">
            <select aria-label="Members data access" className="field" onChange={(event) => update({ memberScope: event.target.value })} value={draft.memberScope}>
              {dataScopes.map((scope) => <option key={scope} value={scope}>{scopeLabel(scope)}</option>)}
            </select>
          </FormField>
          {kind === "accountant" ? (
            <FormField label="Financial data access">
              <select aria-label="Financial data access" className="field" onChange={(event) => update({ paymentScope: event.target.value })} value={draft.paymentScope}>
                {dataScopes.map((scope) => <option key={scope} value={scope}>{scopeLabel(scope)}</option>)}
              </select>
            </FormField>
          ) : null}
        </>
      ) : (
        <p className="text-sm text-muted">Permission overrides are available only when your account can manage Roles & Permissions.</p>
      )}
    </>
  );
}

export function ReviewStep({
  draft,
  kind,
  roleLabel,
  selectedRole,
  seats,
  candidate,
  mode,
  currentPlanName,
}: {
  draft: StaffDraft;
  kind: string;
  roleLabel: string;
  selectedRole: AccessRoleList["roles"][number] | null;
  seats: WorkspacePlan[];
  candidate?: Candidate | null;
  mode: "create" | "edit";
  currentPlanName?: string | null;
}) {
  const seat = seats.find((plan) => plan.subscriptionId === draft.subscriptionId);
  return (
    <div className="grid gap-3 rounded-xl border border-line p-4 text-sm">
      <p><span className="text-muted">Name:</span> {draft.fullName}</p>
      <p><span className="text-muted">Email / mobile:</span> {draft.email} · {draft.phone}</p>
      <p><span className="text-muted">Job:</span> {draft.designation} · {employmentTypeLabel(draft.employmentType)} · joins {draft.joiningDate}</p>
      {mode === "edit" ? <p><span className="text-muted">Employment status:</span> {draft.employmentStatus}</p> : null}
      <p><span className="text-muted">Role:</span> {roleLabel}</p>
      <p><span className="text-muted">Seat:</span> {seat ? `${seat.planName} (${seat.available} left)` : currentPlanName ? `Keep ${currentPlanName}` : "None selected"}</p>
      <p><span className="text-muted">Permissions:</span> {selectedRole?.permissionSummary || "Inherited from the selected role"}</p>
      {kind === "trainer" ? <p><span className="text-muted">Coach profile:</span> {draft.specialization || "General"} · {draft.experienceYears || "0"} yrs</p> : null}
      {mode === "create" ? (
        candidate && draft.linkExistingUser
          ? <p><span className="text-muted">Account:</span> Link existing user {candidate.email}</p>
          : <p><span className="text-muted">Account:</span> {draft.sendActivationLink ? "An activation link will be emailed. No password is shared with you." : "No activation email will be sent."}</p>
      ) : (
        <p><span className="text-muted">Account:</span> Existing workspace user. Email cannot be changed here.</p>
      )}
      {mode === "create" ? <p className="text-muted">After creation you can optionally set up compensation on Payroll.</p> : null}
    </div>
  );
}
