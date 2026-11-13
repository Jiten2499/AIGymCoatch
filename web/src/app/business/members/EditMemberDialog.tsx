"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { FormDialog } from "../../../components/FormDialog";
import { api, apiBlob, apiRoot, ApiError } from "../../../lib/api";
import { getSession } from "../../../lib/session";
import type { BranchOption, GymMemberDetails, GymMembership, MembershipPackage } from "../../../lib/gym";
import { nameInitials } from "../../../lib/initials";
import type { Page } from "../../../lib/types";
import { draftFromMember, editError, memberUpdateBody, stepError, type MemberDraft } from "./memberWizard";
import { MemberReview, MembershipPackageStep, type MembershipTerm, type PtPackageOption } from "./MemberPackageStep";
import { BasicStep, ContactStep, GymStep } from "./MemberWizardSteps";

type TrainerOption = { id: string; firstName: string; lastName: string; status: string };
type PtAssignment = { packageId: string; startDate: string; status: string };

const photoTypes = new Set(["image/jpeg", "image/png", "image/webp"]);

export function EditMemberDialog({ memberId, onCancel, onSaved }: { memberId: string; onCancel: () => void; onSaved: () => void }) {
  const member = useQuery({ queryKey: ["member", "edit", memberId], queryFn: () => api<GymMemberDetails>(`/members/${memberId}`) });
  if (!member.data) {
    return (
      <FormDialog onCancel={onCancel} title="Edit member" wide>
        {member.isLoading ? <p className="mt-4" role="status">Loading member.</p> : null}
        {member.error instanceof ApiError ? <p className="mt-4" role="alert">{member.error.message}</p> : null}
      </FormDialog>
    );
  }
  return <EditWizard key={member.data.id} member={member.data} memberId={memberId} onCancel={onCancel} onSaved={onSaved} />;
}

function EditWizard({ member, memberId, onCancel, onSaved }: { member: GymMemberDetails; memberId: string; onCancel: () => void; onSaved: () => void }) {
  const client = useQueryClient();
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<MemberDraft>(() => draftFromMember(member));
  const [photo, setPhoto] = useState<File | null>(null);
  const [removePhoto, setRemovePhoto] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [filled, setFilled] = useState(false);
  const packages = useQuery({ queryKey: ["membership-plans", "member-edit"], queryFn: () => api<Page<MembershipPackage>>("/membership-plans?page=1&pageSize=100") });
  const branches = useQuery({ queryKey: ["branches", "member-edit"], queryFn: () => api<Page<BranchOption>>("/branches?page=1&pageSize=50") });
  const trainers = useQuery({ queryKey: ["trainers", "member-edit"], queryFn: () => api<Page<TrainerOption>>("/trainers?page=1&pageSize=50") });
  const ptPackages = useQuery({ queryKey: ["pt-packages", "member-edit"], queryFn: () => api<PtPackageOption[]>("/pt-packages") });
  const memberships = useQuery({ queryKey: ["memberships", memberId, "edit"], queryFn: () => api<Page<GymMembership>>(`/memberships?memberId=${memberId}&page=1&pageSize=20`) });
  const assignments = useQuery({ queryKey: ["member-pt", memberId, "edit"], queryFn: () => api<PtAssignment[]>(`/members/${memberId}/pt-assignments`) });
  const term = useQuery({
    queryKey: ["membership-term", draft.planId, draft.startDate],
    enabled: Boolean(draft.planId && draft.startDate),
    queryFn: () => api<MembershipTerm>(`/membership-plans/${draft.planId}/term?start=${draft.startDate}`),
  });
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

  useEffect(() => {
    if (filled || !memberships.isSuccess || !assignments.isSuccess) {
      return;
    }
    const membership = currentMembership(memberships.data.items);
    const assignment = currentAssignment(assignments.data);
    setDraft((current) => ({
      ...current,
      planId: membership?.membershipPlanId ?? "",
      branchId: membership?.branchId ?? "",
      startDate: membership?.startDate ?? current.startDate,
      ptPackageId: assignment?.packageId ?? "",
    }));
    setFilled(true);
  }, [assignments.data, assignments.isSuccess, filled, memberships.data, memberships.isSuccess]);

  const selected = (packages.data?.items ?? []).find((plan) => plan.id === draft.planId) ?? null;
  const selectedPt = (ptPackages.data ?? []).find((item) => item.id === draft.ptPackageId) ?? null;
  const activeTrainers = (trainers.data?.items ?? []).filter((trainer) => trainer.status === "Active" || trainer.id === draft.trainerId);
  const trainerName = activeTrainers.find((trainer) => trainer.id === draft.trainerId);
  const currentPhoto = photo ?? (removePhoto ? null : existing.data ?? null);

  function update(patch: Partial<MemberDraft>) {
    setMessage(null);
    setDraft((current) => ({ ...current, ...patch }));
  }

  function next() {
    const error = step === 4 ? null : stepError(step, draft);
    if (error) {
      setMessage(error);
      return;
    }
    setMessage(null);
    setStep((current) => current + 1);
  }

  function choosePhoto(file: File | null) {
    setRemovePhoto(false);
    setPhoto(file);
    setMessage(file && (!photoTypes.has(file.type) || file.size > 2_097_152) ? "Choose a JPEG, PNG, or WebP image under 2 MB." : null);
  }

  return (
    <FormDialog onCancel={onCancel} title={`Edit member · Step ${step} of 5`} wide>
    <div className="mt-4 grid gap-3">
      {step === 1 ? (
        <BasicStep
          draft={draft}
          emailLocked
          onPhoto={choosePhoto}
          photoName={photo?.name ?? (removePhoto ? "Photo will be removed" : member.hasPhoto ? "Current photo" : "")}
          photoPreview={(
            <div className="flex items-center gap-4">
              <PhotoFace file={currentPhoto} initials={nameInitials(member.firstName, member.lastName)} />
              {member.hasPhoto || photo ? <button className="min-h-11 rounded-full border border-line px-4" onClick={() => { setPhoto(null); setRemovePhoto(true); }} type="button">Remove photo</button> : null}
            </div>
          )}
          update={update}
        />
      ) : null}
      {step === 2 ? <ContactStep draft={draft} update={update} /> : null}
      {step === 3 ? <GymStep draft={draft} showAppAccess={false} trainers={activeTrainers} update={update} /> : null}
      {step === 4 ? (
        <MembershipPackageStep
          branches={branches.data?.items ?? []}
          draft={draft}
          plans={packages.data?.items ?? []}
          ptPackages={ptPackages.data ?? []}
          readOnly
          term={term.data}
          update={update}
        />
      ) : null}
      {step === 5 ? <MemberReview draft={draft} editing plan={selected} ptPackage={selectedPt} term={term.data ?? null} trainerName={trainerName ? `${trainerName.firstName} ${trainerName.lastName}` : ""} /> : null}
      {message ? <p role="alert">{message}</p> : null}
      {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
      <div className="mt-2 flex items-center justify-between gap-3">
        <button className="min-h-11 rounded-full border border-line px-4" disabled={save.isPending} onClick={onCancel} type="button">Cancel</button>
        <div className="flex gap-3">
          {step > 1 ? <button className="min-h-11 rounded-full px-4" disabled={save.isPending} onClick={() => setStep((current) => current - 1)} type="button">Previous</button> : null}
          {step < 5 ? (
            <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={next} type="button">Next</button>
          ) : (
            <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={save.isPending} onClick={() => { const error = editError(draft); if (error) { setMessage(error); return; } save.mutate(); }} type="button">{save.isPending ? "Saving..." : "Save member"}</button>
          )}
        </div>
      </div>
    </div>
    </FormDialog>
  );
}

function currentMembership(items: GymMembership[]) {
  return [...items].sort((left, right) => Number(right.status === "Active") - Number(left.status === "Active") || right.startDate.localeCompare(left.startDate))[0];
}

function currentAssignment(items: PtAssignment[]) {
  return [...items].sort((left, right) => Number(right.status === "Active") - Number(left.status === "Active") || right.startDate.localeCompare(left.startDate))[0];
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
  useEffect(() => () => { if (src) URL.revokeObjectURL(src); }, [src]);
  if (!src) {
    return <span className="inline-flex h-20 w-20 items-center justify-center rounded-full bg-background text-lg font-semibold">{initials}</span>;
  }
  return <img alt="" className="h-20 w-20 rounded-full object-cover" src={src} />;
}
