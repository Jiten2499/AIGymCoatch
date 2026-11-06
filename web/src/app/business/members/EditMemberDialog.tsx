"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { FormDialog } from "../../../components/FormDialog";
import { api, apiBlob, apiRoot, ApiError } from "../../../lib/api";
import { getSession } from "../../../lib/session";
import type { GymMemberDetails } from "../../../lib/gym";
import { nameInitials } from "../../../lib/initials";
import type { Page } from "../../../lib/types";
import { draftFromMember, editError, fitnessGoals, memberGenders, memberStatuses, memberUpdateBody, type MemberDraft } from "./memberWizard";

type TrainerOption = { id: string; firstName: string; lastName: string; status: string };

const photoTypes = new Set(["image/jpeg", "image/png", "image/webp"]);

export function EditMemberDialog({ memberId, onCancel, onSaved }: { memberId: string; onCancel: () => void; onSaved: () => void }) {
  const member = useQuery({ queryKey: ["member", "edit", memberId], queryFn: () => api<GymMemberDetails>(`/members/${memberId}`) });
  return (
    <FormDialog onCancel={onCancel} title="Edit member" wide>
      {member.isLoading ? <p className="mt-4" role="status">Loading member.</p> : null}
      {member.error instanceof ApiError ? <p className="mt-4" role="alert">{member.error.message}</p> : null}
      {member.data ? <EditForm key={member.data.id} member={member.data} memberId={memberId} onCancel={onCancel} onSaved={onSaved} /> : null}
    </FormDialog>
  );
}

function EditForm({ member, memberId, onCancel, onSaved }: { member: GymMemberDetails; memberId: string; onCancel: () => void; onSaved: () => void }) {
  const client = useQueryClient();
  const [draft, setDraft] = useState<MemberDraft>(() => draftFromMember(member));
  const [photo, setPhoto] = useState<File | null>(null);
  const [removePhoto, setRemovePhoto] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const trainers = useQuery({ queryKey: ["trainers", "member-edit"], queryFn: () => api<Page<TrainerOption>>("/trainers?page=1&pageSize=50") });
  const existing = useQuery({
    queryKey: ["member-photo", memberId],
    enabled: member.hasPhoto && !photo && !removePhoto,
    retry: false,
    queryFn: () => apiBlob(`/members/${memberId}/profile-photo`),
  });
  const save = useMutation({
    mutationFn: async () => {
      const error = editError(draft);
      if (error) {
        throw new ApiError(error, 400);
      }
      if (photo && (!photoTypes.has(photo.type) || photo.size === 0 || photo.size > 2_097_152)) {
        throw new ApiError("Choose a JPEG, PNG, or WebP image under 2 MB.", 400);
      }
      await api(`/members/${memberId}`, { method: "PUT", body: JSON.stringify(memberUpdateBody(draft)) });
      if (photo) {
        await uploadMemberPhoto(memberId, photo);
      } else if (removePhoto && member.hasPhoto) {
        await api(`/members/${memberId}/profile-photo`, { method: "DELETE" });
      }
    },
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["members"] });
      await client.invalidateQueries({ queryKey: ["member"] });
      await client.invalidateQueries({ queryKey: ["member-photo", memberId] });
      onSaved();
    },
  });

  const activeTrainers = (trainers.data?.items ?? []).filter((trainer) => trainer.status === "Active" || trainer.id === draft.trainerId);
  const currentPhoto = photo ?? (removePhoto ? null : existing.data ?? null);

  function update(patch: Partial<MemberDraft>) {
    setMessage(null);
    setDraft((current) => ({ ...current, ...patch }));
  }

  return (
    <form className="mt-4 grid gap-3 sm:grid-cols-2" onSubmit={(event) => { event.preventDefault(); setMessage(null); save.mutate(); }}>
      <div className="flex items-center gap-4 sm:col-span-2">
        <PhotoFace file={currentPhoto} initials={nameInitials(member.firstName, member.lastName)} />
        <div className="grid gap-2">
          <label className="grid gap-2">Profile photo
            <input accept="image/jpeg,image/png,image/webp" aria-label="Profile photo" className="field" onChange={(event) => { const file = event.target.files?.[0] ?? null; setRemovePhoto(false); setPhoto(file); setMessage(file && (!photoTypes.has(file.type) || file.size > 2_097_152) ? "Choose a JPEG, PNG, or WebP image under 2 MB." : null); }} type="file" />
          </label>
          {member.hasPhoto || photo ? <button className="min-h-11 justify-self-start rounded-full border border-line px-4" onClick={() => { setPhoto(null); setRemovePhoto(true); }} type="button">Remove photo</button> : null}
        </div>
      </div>
      <Field text="Full name"><input className="field" onChange={(event) => update({ fullName: event.target.value })} value={draft.fullName} /></Field>
      <Field text="Mobile number"><input className="field" onChange={(event) => update({ phone: event.target.value })} value={draft.phone} /></Field>
      <Field text="Email"><input className="field" disabled value={draft.email} /></Field>
      <Field text="Date of birth"><input className="field" onChange={(event) => update({ dateOfBirth: event.target.value })} type="date" value={draft.dateOfBirth} /></Field>
      <Field text="Gender"><Select onChange={(gender) => update({ gender })} options={memberGenders} value={draft.gender} /></Field>
      <Field text="Member status"><Select onChange={(status) => update({ status: status as MemberDraft["status"] })} options={memberStatuses} value={draft.status} /></Field>
      <Field text="Joining date"><input className="field" onChange={(event) => update({ joinDate: event.target.value })} type="date" value={draft.joinDate} /></Field>
      <Field text="Fitness goal"><Select onChange={(fitnessGoal) => update({ fitnessGoal })} options={fitnessGoals} value={draft.fitnessGoal} /></Field>
      <Field text="Assigned trainer">
        <select className="field" onChange={(event) => update({ trainerId: event.target.value })} value={draft.trainerId}>
          <option value="">None</option>
          {activeTrainers.map((trainer) => <option key={trainer.id} value={trainer.id}>{trainer.firstName} {trainer.lastName}</option>)}
        </select>
      </Field>
      <Field text="Address"><input className="field" onChange={(event) => update({ address: event.target.value })} value={draft.address} /></Field>
      <Field text="City"><input className="field" onChange={(event) => update({ city: event.target.value })} value={draft.city} /></Field>
      <Field text="State"><input className="field" onChange={(event) => update({ state: event.target.value })} value={draft.state} /></Field>
      <Field text="Postal code"><input className="field" onChange={(event) => update({ postalCode: event.target.value })} value={draft.postalCode} /></Field>
      <Field text="Emergency contact name"><input className="field" onChange={(event) => update({ emergencyName: event.target.value })} value={draft.emergencyName} /></Field>
      <Field text="Emergency contact mobile"><input className="field" onChange={(event) => update({ emergencyPhone: event.target.value })} value={draft.emergencyPhone} /></Field>
      <Field text="Emergency relationship"><input className="field" onChange={(event) => update({ emergencyRelationship: event.target.value })} value={draft.emergencyRelationship} /></Field>
      {message ? <p className="sm:col-span-2" role="alert">{message}</p> : null}
      {save.error instanceof ApiError ? <p className="sm:col-span-2" role="alert">{save.error.message}</p> : null}
      <div className="mt-2 flex justify-end gap-3 sm:col-span-2">
        <button className="min-h-11 rounded-full border border-line px-4" disabled={save.isPending} onClick={onCancel} type="button">Cancel</button>
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={save.isPending} type="submit">{save.isPending ? "Saving..." : "Save member"}</button>
      </div>
    </form>
  );
}

async function uploadMemberPhoto(memberId: string, file: File) {
  const body = new FormData();
  body.append("file", file);
  const session = getSession();
  const response = await fetch(`${apiRoot()}/members/${memberId}/profile-photo`, {
    method: "POST",
    headers: session ? { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" } : { Accept: "application/json" },
    body,
  });
  if (!response.ok) {
    throw new ApiError("The member was saved, but the photo was not.", response.status);
  }
}

function PhotoFace({ file, initials }: { file: Blob | null; initials: string }) {
  const src = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);
  useEffect(() => () => {
    if (src) {
      URL.revokeObjectURL(src);
    }
  }, [src]);
  if (!src) {
    return <span className="inline-flex h-20 w-20 items-center justify-center rounded-full bg-background text-lg font-semibold">{initials}</span>;
  }
  return <img alt="" className="h-20 w-20 rounded-full object-cover" src={src} />;
}

function Field({ text, children }: { text: string; children: React.ReactNode }) {
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
