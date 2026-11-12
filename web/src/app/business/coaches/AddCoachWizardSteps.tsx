"use client";

import { FormField } from "../../../components/FormDialog";
import type { WorkspaceUser } from "../../../lib/types";
import { CoachPhotoField } from "./CoachPhotoField";
import {
  coachDesignations,
  coachEmploymentTypes,
  coachGenders,
  coachSpecializations,
  emptyCert,
  employmentTypeLabel,
  needsCommission,
  needsMonthlySalary,
  needsPerSessionRate,
  paymentFrequencies,
  salaryTypeLabel,
  salaryTypes,
  type CoachDraft,
} from "./coachWizard";

export type BranchOption = { id: string; name: string; status?: string };
export type Candidate = { userId: string; email: string; firstName: string; lastName: string; phone: string | null; alreadyInWorkspace: boolean };

export function CoachStepIndicator({ step, includeCompensation }: { step: number; includeCompensation: boolean }) {
  const labels = includeCompensation
    ? ["Personal", "Professional", "Employment", "Compensation", "Access"]
    : ["Personal", "Professional", "Employment", "Access"];
  const displayStep = !includeCompensation && step === 5 ? 4 : step;
  return (
    <ol className={`mb-2 grid gap-2 text-sm ${includeCompensation ? "grid-cols-2 sm:grid-cols-5" : "grid-cols-2 sm:grid-cols-4"}`}>
      {labels.map((label, index) => {
        const current = index + 1;
        return (
          <li className={`rounded-full border px-3 py-2 text-center ${current === displayStep ? "border-accent bg-accent/10 font-semibold" : "border-line text-muted"}`} key={label}>
            {current}. {label}
          </li>
        );
      })}
    </ol>
  );
}

export function PersonalStep({
  draft,
  photo,
  photoError,
  existingPhoto,
  emailLocked,
  update,
  onPhoto,
  onRemovePhoto,
}: {
  draft: CoachDraft;
  photo: File | null;
  photoError: string | null;
  existingPhoto?: Blob | null;
  emailLocked?: boolean;
  update: (patch: Partial<CoachDraft>) => void;
  onPhoto: (file: File | null, error: string | null) => void;
  onRemovePhoto?: () => void;
}) {
  return (
    <section className="grid gap-3">
      <h3 className="text-sm font-semibold tracking-[0.12em] text-muted uppercase">Personal information</h3>
      <CoachPhotoField
        existing={existingPhoto ?? null}
        file={photo}
        firstName={draft.firstName}
        lastName={draft.lastName}
        onFile={onPhoto}
        onRemove={() => {
          onPhoto(null, null);
          onRemovePhoto?.();
        }}
        removed={!photo && !existingPhoto}
      />
      {photoError ? <p role="alert">{photoError}</p> : null}
      <div className="grid gap-3 md:grid-cols-3">
        <FormField label="First name *"><input className="field" onChange={(event) => update({ firstName: event.target.value })} value={draft.firstName} /></FormField>
        <FormField label="Middle name"><input className="field" onChange={(event) => update({ middleName: event.target.value })} value={draft.middleName} /></FormField>
        <FormField label="Last name *"><input className="field" onChange={(event) => update({ lastName: event.target.value })} value={draft.lastName} /></FormField>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <FormField label="Email address *"><input className="field" disabled={emailLocked} onChange={(event) => update({ email: event.target.value, linkExistingUser: false })} type="email" value={draft.email} /></FormField>
        <FormField label="Mobile number *"><input className="field" onChange={(event) => update({ phone: event.target.value, linkExistingUser: false })} placeholder="+977 98XXXXXXXX" value={draft.phone} /></FormField>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <FormField label="Gender">
          <select aria-label="Gender" className="field" onChange={(event) => update({ gender: event.target.value })} value={draft.gender}>
            {coachGenders.map((item) => <option key={item || "none"} value={item}>{item || "Prefer not to say"}</option>)}
          </select>
        </FormField>
        <FormField label="Date of birth"><input className="field" onChange={(event) => update({ dateOfBirth: event.target.value })} type="date" value={draft.dateOfBirth} /></FormField>
      </div>
      <FormField label="Address"><input className="field" onChange={(event) => update({ address: event.target.value })} value={draft.address} /></FormField>
      <div className="grid gap-3 md:grid-cols-2">
        <FormField label="Emergency contact name"><input className="field" onChange={(event) => update({ emergencyName: event.target.value })} value={draft.emergencyName} /></FormField>
        <FormField label="Emergency contact number"><input className="field" onChange={(event) => update({ emergencyPhone: event.target.value })} value={draft.emergencyPhone} /></FormField>
      </div>
    </section>
  );
}

export function ProfessionalStep({ draft, update }: { draft: CoachDraft; update: (patch: Partial<CoachDraft>) => void }) {
  function toggleSpecialization(value: string) {
    const selected = draft.specializations.includes(value)
      ? draft.specializations.filter((item) => item !== value)
      : [...draft.specializations, value];
    update({ specializations: selected });
  }

  return (
    <section className="grid gap-3">
      <h3 className="text-sm font-semibold tracking-[0.12em] text-muted uppercase">Professional information</h3>
      <FormField label="Coach ID"><input className="field" disabled readOnly value={draft.employeeCode.trim() || "Auto-generated on save"} /></FormField>
      <FormField label="Designation *">
        <select aria-label="Designation" className="field" onChange={(event) => update({ designation: event.target.value })} value={draft.designation}>
          {coachDesignations.map((item) => <option key={item} value={item}>{item}</option>)}
        </select>
      </FormField>
      <div>
        <p className="mb-2">Specializations</p>
        <div className="flex flex-wrap gap-2">
          {coachSpecializations.map((item) => {
            const active = draft.specializations.includes(item);
            return (
              <button
                className={`min-h-11 rounded-full border px-3 ${active ? "border-accent bg-accent/10 font-semibold" : "border-line"}`}
                key={item}
                onClick={() => toggleSpecialization(item)}
                type="button"
              >
                {item}
              </button>
            );
          })}
        </div>
      </div>
      <FormField label="Experience in years"><input className="field" inputMode="numeric" onChange={(event) => update({ experienceYears: event.target.value })} value={draft.experienceYears} /></FormField>
      <FormField label="Bio / introduction"><textarea className="field min-h-24" onChange={(event) => update({ bio: event.target.value })} value={draft.bio} /></FormField>
      <FormField label="Previous workplace"><input className="field" onChange={(event) => update({ previousWorkplace: event.target.value })} value={draft.previousWorkplace} /></FormField>
      <div className="grid gap-3">
        <div className="flex items-center justify-between gap-3">
          <p className="font-semibold">Certifications</p>
          <button className="min-h-11 rounded-full border border-line px-3" onClick={() => update({ certifications: [...draft.certifications, emptyCert()] })} type="button">Add certification</button>
        </div>
        {draft.certifications.length === 0 ? <p className="text-sm text-muted">Optional. Add certificate name, issuer, and dates.</p> : null}
        {draft.certifications.map((cert, index) => (
          <div className="grid gap-3 rounded-2xl border border-line p-4" key={`cert-${index}`}>
            <div className="grid gap-3 md:grid-cols-2">
              <FormField label="Certificate name"><input className="field" onChange={(event) => update({ certifications: draft.certifications.map((row, i) => i === index ? { ...row, name: event.target.value } : row) })} value={cert.name} /></FormField>
              <FormField label="Issuing organization"><input className="field" onChange={(event) => update({ certifications: draft.certifications.map((row, i) => i === index ? { ...row, organization: event.target.value } : row) })} value={cert.organization} /></FormField>
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <FormField label="Issue date"><input className="field" onChange={(event) => update({ certifications: draft.certifications.map((row, i) => i === index ? { ...row, issueDate: event.target.value } : row) })} type="date" value={cert.issueDate} /></FormField>
              <FormField label="Expiry date"><input className="field" onChange={(event) => update({ certifications: draft.certifications.map((row, i) => i === index ? { ...row, expiryDate: event.target.value } : row) })} type="date" value={cert.expiryDate} /></FormField>
            </div>
            <button className="min-h-11 justify-self-start rounded-full border border-line px-3" onClick={() => update({ certifications: draft.certifications.filter((_, i) => i !== index) })} type="button">Remove</button>
          </div>
        ))}
      </div>
    </section>
  );
}

export function EmploymentStep({
  draft,
  branches,
  managers,
  update,
}: {
  draft: CoachDraft;
  branches: BranchOption[];
  managers: WorkspaceUser[];
  update: (patch: Partial<CoachDraft>) => void;
}) {
  return (
    <section className="grid gap-3">
      <h3 className="text-sm font-semibold tracking-[0.12em] text-muted uppercase">Employment &amp; availability</h3>
      <div className="grid gap-3 md:grid-cols-2">
        <FormField label="Employment type *">
          <select aria-label="Employment type" className="field" onChange={(event) => update({ employmentType: event.target.value })} value={draft.employmentType}>
            {coachEmploymentTypes.map((item) => <option key={item} value={item}>{employmentTypeLabel(item)}</option>)}
          </select>
        </FormField>
        <FormField label="Joining date *"><input className="field" onChange={(event) => update({ joiningDate: event.target.value })} type="date" value={draft.joiningDate} /></FormField>
      </div>
      {branches.length > 0 ? (
        <FormField label="Assigned branch">
          <select aria-label="Assigned branch" className="field" onChange={(event) => update({ branchId: event.target.value })} value={draft.branchId}>
            <option value="">No branch</option>
            {branches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
          </select>
        </FormField>
      ) : null}
      <FormField label="Reporting manager">
        <select aria-label="Reporting manager" className="field" onChange={(event) => update({ reportingManagerUserId: event.target.value })} value={draft.reportingManagerUserId}>
          <option value="">None</option>
          {managers.map((manager) => <option key={manager.id} value={manager.id}>{manager.firstName} {manager.lastName}</option>)}
        </select>
      </FormField>
      <FormField label="Employment status">
        <select aria-label="Employment status" className="field" onChange={(event) => update({ employmentStatus: event.target.value })} value={draft.employmentStatus}>
          <option value="Active">Active</option>
          <option value="Inactive">Inactive</option>
        </select>
      </FormField>
    </section>
  );
}

export function CompensationStep({ draft, update }: { draft: CoachDraft; update: (patch: Partial<CoachDraft>) => void }) {
  return (
    <section className="grid gap-3">
      <h3 className="text-sm font-semibold tracking-[0.12em] text-muted uppercase">Compensation &amp; incentives</h3>
      <p className="text-sm text-muted">Base pay is saved to payroll after the coach is created. Commission rules can be refined on the coach profile.</p>
      <FormField label="Salary type">
        <select
          aria-label="Salary type"
          className="field"
          onChange={(event) => {
            const salaryType = event.target.value;
            update({ salaryType, commissionEligible: salaryType === "CommissionBased" || salaryType === "FixedPlusCommission" });
          }}
          value={draft.salaryType}
        >
          {salaryTypes.map((item) => <option key={item} value={item}>{salaryTypeLabel(item)}</option>)}
        </select>
      </FormField>
      {needsMonthlySalary(draft.salaryType) ? (
        <FormField label="Monthly salary"><input className="field" inputMode="decimal" onChange={(event) => update({ monthlySalary: event.target.value })} value={draft.monthlySalary} /></FormField>
      ) : null}
      {needsPerSessionRate(draft.salaryType) ? (
        <FormField label="Per-session rate"><input className="field" inputMode="decimal" onChange={(event) => update({ perSessionRate: event.target.value })} value={draft.perSessionRate} /></FormField>
      ) : null}
      {needsCommission(draft.salaryType) ? (
        <FormField label="Commission percentage"><input className="field" inputMode="decimal" onChange={(event) => update({ commissionPercent: event.target.value })} value={draft.commissionPercent} /></FormField>
      ) : null}
      <FormField label="Payment frequency">
        <select aria-label="Payment frequency" className="field" onChange={(event) => update({ paymentFrequency: event.target.value })} value={draft.paymentFrequency}>
          {paymentFrequencies.map((item) => <option key={item} value={item}>{item}</option>)}
        </select>
      </FormField>
      <label className="flex min-h-11 items-center gap-3 text-sm">
        <input checked={draft.commissionEligible} className="size-4" onChange={(event) => update({ commissionEligible: event.target.checked })} type="checkbox" />
        Eligible for commission and performance incentives
      </label>
    </section>
  );
}

export function AccessReviewStep({
  draft,
  candidate,
  mode = "create",
  seatChoices,
  seatRequired,
  update,
}: {
  draft: CoachDraft;
  candidate: Candidate | null;
  mode?: "create" | "edit";
  seatChoices: { subscriptionId: string; planName: string; available: number }[];
  seatRequired?: boolean;
  update: (patch: Partial<CoachDraft>) => void;
}) {
  const selectedSeat = seatChoices.find((plan) => plan.subscriptionId === draft.subscriptionId);
  return (
    <section className="grid gap-3">
      <h3 className="text-sm font-semibold tracking-[0.12em] text-muted uppercase">Account access &amp; review</h3>
      <p>Login email: <strong>{draft.email.trim() || "—"}</strong> · Role: Coach (Trainer)</p>
      <p className="text-sm text-muted">Seat allocation is separate from role permissions. Keeping the current seat does not consume another seat.</p>
      <FormField label={seatRequired ? "Business seat *" : "Business seat"}>
        <select aria-label="Business seat" className="field" onChange={(event) => update({ subscriptionId: event.target.value })} value={draft.subscriptionId}>
          <option value="">{mode === "edit" ? "Keep current seat / none" : seatChoices.length ? "Select a seat plan" : "No seats available"}</option>
          {seatChoices.map((plan) => <option key={plan.subscriptionId} value={plan.subscriptionId}>{plan.planName} · {plan.available} seats available</option>)}
        </select>
      </FormField>
      {mode === "edit" && draft.planName && !draft.subscriptionId ? <p className="text-sm text-muted">Current plan: {draft.planName}</p> : null}
      {mode === "create" && candidate?.alreadyInWorkspace ? <p role="alert">That person already belongs to this workspace.</p> : null}
      {mode === "create" && candidate && !candidate.alreadyInWorkspace ? <p role="status">An existing LifeFit account was found and will be reused for this coach.</p> : null}
      {mode === "create" && !candidate ? <p className="text-sm text-muted">A new login will be created if this email is not registered yet.</p> : null}
      {mode === "edit" ? <p className="text-sm text-muted">Email stays with the existing LifeFit account.</p> : null}
      {mode === "create" ? (
        <label className="flex min-h-11 items-center gap-3 text-sm">
          <input checked={draft.sendActivationLink} className="size-4" onChange={(event) => update({ sendActivationLink: event.target.checked })} type="checkbox" />
          Send activation / invitation email after create
        </label>
      ) : null}
      <div className="rounded-2xl border border-line p-4 text-sm">
        <p className="font-semibold">Summary</p>
        <p className="mt-2">{draft.firstName} {draft.middleName} {draft.lastName}</p>
        <p>{draft.designation} · {employmentTypeLabel(draft.employmentType)} · {draft.employmentStatus}</p>
        <p>{draft.specializations.join(", ") || "No specializations selected"} · Joining {draft.joiningDate || "—"}</p>
        <p>Seat: {selectedSeat?.planName || draft.planName || "Not assigned"}</p>
      </div>
    </section>
  );
}
