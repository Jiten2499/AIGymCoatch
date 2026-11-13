"use client";

import { AppAccessFields } from "../../../components/AppAccessFields";
import { fitnessGoals, memberGenders, memberStatuses, type MemberDraft } from "./memberWizard";

type TrainerOption = { id: string; firstName: string; lastName: string; status: string };

export function BasicStep({
  draft,
  photoName,
  emailLocked,
  update,
  onPhoto,
  photoPreview,
}: {
  draft: MemberDraft;
  photoName: string;
  emailLocked?: boolean;
  update: (patch: Partial<MemberDraft>) => void;
  onPhoto: (file: File | null) => void;
  photoPreview?: React.ReactNode;
}) {
  return (
    <>
      {photoPreview}
      <Label text="Full name"><input className="field" onChange={(event) => update({ fullName: event.target.value })} value={draft.fullName} /></Label>
      <Label text="Mobile number"><input className="field" onChange={(event) => update({ phone: event.target.value })} value={draft.phone} /></Label>
      <Label text="Email"><input className="field" disabled={emailLocked} onChange={(event) => update({ email: event.target.value })} type="email" value={draft.email} /></Label>
      <Label text="Date of birth"><input className="field" onChange={(event) => update({ dateOfBirth: event.target.value })} type="date" value={draft.dateOfBirth} /></Label>
      <Label text="Gender"><Select onChange={(gender) => update({ gender })} options={memberGenders} value={draft.gender} /></Label>
      <Label text="Profile photo"><input accept="image/jpeg,image/png,image/webp" aria-label="Profile photo" className="field" onChange={(event) => onPhoto(event.target.files?.[0] ?? null)} type="file" /></Label>
      {photoName ? <p className="text-sm text-muted">{photoName}</p> : null}
    </>
  );
}

export function ContactStep({ draft, update }: { draft: MemberDraft; update: (patch: Partial<MemberDraft>) => void }) {
  return (
    <>
      <Label text="Address"><input className="field" onChange={(event) => update({ address: event.target.value })} value={draft.address} /></Label>
      <Label text="City"><input className="field" onChange={(event) => update({ city: event.target.value })} value={draft.city} /></Label>
      <Label text="State"><input className="field" onChange={(event) => update({ state: event.target.value })} value={draft.state} /></Label>
      <Label text="Postal code"><input className="field" onChange={(event) => update({ postalCode: event.target.value })} value={draft.postalCode} /></Label>
      <Label text="Emergency contact name"><input className="field" onChange={(event) => update({ emergencyName: event.target.value })} value={draft.emergencyName} /></Label>
      <Label text="Emergency contact mobile"><input className="field" onChange={(event) => update({ emergencyPhone: event.target.value })} value={draft.emergencyPhone} /></Label>
      <Label text="Emergency relationship"><input className="field" onChange={(event) => update({ emergencyRelationship: event.target.value })} value={draft.emergencyRelationship} /></Label>
    </>
  );
}

export function GymStep({
  draft,
  trainers,
  update,
  showAppAccess = true,
}: {
  draft: MemberDraft;
  trainers: TrainerOption[];
  update: (patch: Partial<MemberDraft>) => void;
  showAppAccess?: boolean;
}) {
  return (
    <>
      <Label text="Joining date"><input className="field" onChange={(event) => update({ joinDate: event.target.value })} type="date" value={draft.joinDate} /></Label>
      <Label text="Member status"><Select onChange={(status) => update({ status: status as MemberDraft["status"] })} options={memberStatuses} value={draft.status} /></Label>
      <Label text="Fitness goal"><Select onChange={(fitnessGoal) => update({ fitnessGoal })} options={fitnessGoals} value={draft.fitnessGoal} /></Label>
      <Label text="Assigned trainer">
        <select className="field" onChange={(event) => update({ trainerId: event.target.value })} value={draft.trainerId}>
          <option value="">None</option>
          {trainers.map((trainer) => <option key={trainer.id} value={trainer.id}>{trainer.firstName} {trainer.lastName}</option>)}
        </select>
      </Label>
      {showAppAccess ? (
        <AppAccessFields
          draft={{ enableBasicAppAccess: draft.enableBasicAppAccess, assignAppAccess: draft.assignAppAccess, appSubscriptionId: draft.appSubscriptionId }}
          onChange={(patch) => update(patch)}
          requireEmailHint
        />
      ) : <p className="text-sm text-muted">App login stays with the existing account.</p>}
    </>
  );
}

function Label({ text, children }: { text: string; children: React.ReactNode }) {
  return <label className="grid gap-2">{text}{children}</label>;
}

function Select({ value, options, onChange }: { value: string; options: readonly string[]; onChange: (value: string) => void }) {
  return (
    <select className="field" onChange={(event) => onChange(event.target.value)} value={value}>
      <option value="">Select</option>
      {options.map((option) => <option key={option}>{option}</option>)}
    </select>
  );
}
