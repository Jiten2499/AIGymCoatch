"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { FormDialog, FormField } from "../../../components/FormDialog";
import { api, apiRoot, ApiError } from "../../../lib/api";
import { getSession } from "../../../lib/session";
import type {
  AudiencePreview,
  CommunicationAudienceType,
  CommunicationDetails,
  CommunicationPriority,
  CommunicationType,
  GymMember,
  MembershipPackage,
} from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { ScheduleFields } from "./ScheduleFields";
import { formatWallClock, localDateTimeValue, toDateTimeLocal } from "./schedule";

type PersonOption = { id: string; firstName: string; lastName: string };
type Draft = {
  type: CommunicationType;
  title: string;
  message: string;
  priority: CommunicationPriority;
  audienceType: CommunicationAudienceType;
  audiencePackageId: string;
  audienceTrainerId: string;
  expiringWithinDays: string;
  selectedMemberIds: string[];
  selectedTrainerIds: string[];
  selectedStaffIds: string[];
  recipientSearch: string;
  scheduleMode: "now" | "later" | "draft";
  publishAt: string;
  expiresAt: string;
};

const types: CommunicationType[] = ["Announcement", "Circular", "Offer", "Event", "Alert", "General"];
const priorities: CommunicationPriority[] = ["Low", "Normal", "High"];
const audiences: { value: CommunicationAudienceType; label: string }[] = [
  { value: "AllMembers", label: "All members" },
  { value: "ActiveMembers", label: "Active members" },
  { value: "ExpiringMembers", label: "Expiring memberships" },
  { value: "MembershipPackage", label: "Specific membership package" },
  { value: "TrainerMembers", label: "Specific trainer's members" },
  { value: "SelectedMembers", label: "Selected members" },
  { value: "AllTrainers", label: "All trainers" },
  { value: "AllStaff", label: "All staff" },
  { value: "AllBusinessUsers", label: "All business users" },
  { value: "SelectedTrainers", label: "Selected trainers" },
  { value: "SelectedStaff", label: "Selected staff" },
];

const empty: Draft = {
  type: "Announcement",
  title: "",
  message: "",
  priority: "Normal",
  audienceType: "AllMembers",
  audiencePackageId: "",
  audienceTrainerId: "",
  expiringWithinDays: "7",
  selectedMemberIds: [],
  selectedTrainerIds: [],
  selectedStaffIds: [],
  recipientSearch: "",
  scheduleMode: "draft",
  publishAt: "",
  expiresAt: "",
};

function payload(draft: Draft) {
  return {
    type: draft.type,
    title: draft.title.trim(),
    message: draft.message.trim(),
    priority: draft.priority,
    audienceType: draft.audienceType,
    audiencePackageId: draft.audienceType === "MembershipPackage" ? draft.audiencePackageId || null : null,
    audienceTrainerId: draft.audienceType === "TrainerMembers" ? draft.audienceTrainerId || null : null,
    expiringWithinDays: draft.audienceType === "ExpiringMembers" ? Number(draft.expiringWithinDays || "7") : null,
    selectedMemberIds: draft.audienceType === "SelectedMembers" ? draft.selectedMemberIds : null,
    selectedTrainerIds: draft.audienceType === "SelectedTrainers" ? draft.selectedTrainerIds : null,
    selectedStaffIds: draft.audienceType === "SelectedStaff" ? draft.selectedStaffIds : null,
    publishAt: draft.scheduleMode === "later" ? localDateTimeValue(draft.publishAt) : null,
    expiresAt: localDateTimeValue(draft.expiresAt),
    publishNow: draft.scheduleMode === "now" || draft.scheduleMode === "later",
  };
}

function PersonPicker({ draft, members, trainers, staff, update }: {
  draft: Draft;
  members: GymMember[];
  trainers: PersonOption[];
  staff: PersonOption[];
  update: (patch: Partial<Draft>) => void;
}) {
  const search = draft.recipientSearch.trim().toLowerCase();
  const people = draft.audienceType === "SelectedMembers"
    ? members.map((member) => ({ id: member.id, label: member.fullName }))
    : (draft.audienceType === "SelectedTrainers" ? trainers : staff).map((person) => ({ id: person.id, label: `${person.firstName} ${person.lastName}` }));
  const selected = draft.audienceType === "SelectedMembers"
    ? draft.selectedMemberIds
    : draft.audienceType === "SelectedTrainers" ? draft.selectedTrainerIds : draft.selectedStaffIds;
  const field = draft.audienceType === "SelectedMembers"
    ? "selectedMemberIds"
    : draft.audienceType === "SelectedTrainers" ? "selectedTrainerIds" : "selectedStaffIds";
  return (
    <FormField label="Search and select">
      <input aria-label="Search recipients" className="field" onChange={(event) => update({ recipientSearch: event.target.value })} placeholder="Search by name" value={draft.recipientSearch} />
      <div className="mt-2 max-h-48 overflow-y-auto rounded-xl border border-line p-3">
        {people.filter((person) => person.label.toLowerCase().includes(search)).map((person) => {
          const checked = selected.includes(person.id);
          return (
            <label className="flex min-h-10 items-center gap-2" key={person.id}>
              <input
                checked={checked}
                onChange={() => update({
                  [field]: checked ? selected.filter((id) => id !== person.id) : [...selected, person.id],
                } as Partial<Draft>)}
                type="checkbox"
              />
              {person.label}
            </label>
          );
        })}
      </div>
    </FormField>
  );
}

function draftFrom(details: CommunicationDetails, timeZone: string): Draft {
  return {
    type: details.type,
    title: details.title,
    message: details.message,
    priority: details.priority,
    audienceType: details.audienceType,
    audiencePackageId: details.audiencePackageId ?? "",
    audienceTrainerId: details.audienceTrainerId ?? "",
    expiringWithinDays: String(details.expiringWithinDays ?? 7),
    selectedMemberIds: details.selectedMemberIds,
    selectedTrainerIds: [],
    selectedStaffIds: [],
    recipientSearch: "",
    scheduleMode: details.status === "Scheduled" ? "later" : "draft",
    publishAt: details.publishAt ? toDateTimeLocal(details.publishAt, timeZone) : "",
    expiresAt: details.expiresAt ? toDateTimeLocal(details.expiresAt, timeZone) : "",
  };
}

export function CommunicationWizard({ initial, timeZone = "UTC", onCancel, onSaved }: { initial?: CommunicationDetails | null; timeZone?: string; onCancel: () => void; onSaved: () => void }) {
  const [step, setStep] = useState(1);
  const [file, setFile] = useState<File | null>(null);
  const [draft, setDraft] = useState<Draft>(initial ? draftFrom(initial, timeZone) : empty);
  const packages = useQuery({ queryKey: ["membership-plans", "communications"], queryFn: () => api<Page<MembershipPackage>>("/membership-plans?activeOnly=true&page=1&pageSize=50") });
  const trainers = useQuery({ queryKey: ["trainers", "communications"], queryFn: () => api<Page<PersonOption>>("/trainers?page=1&pageSize=50") });
  const staff = useQuery({ queryKey: ["staff", "communications"], queryFn: () => api<Page<PersonOption>>("/staff?page=1&pageSize=50") });
  const members = useQuery({ queryKey: ["members", "communications"], queryFn: () => api<Page<GymMember>>("/members?page=1&pageSize=100&sort=name") });
  const preview = useQuery({
    queryKey: ["communication-preview", draft],
    enabled: step === 4,
    queryFn: () => api<AudiencePreview>("/communications/audience-preview", { method: "POST", body: JSON.stringify(payload(draft)) }),
  });
  const create = useMutation({
    mutationFn: async () => {
      const saved = await api<CommunicationDetails>(initial ? `/communications/${initial.id}` : "/communications", {
        method: initial ? "PUT" : "POST",
        body: JSON.stringify(payload(draft)),
      });
      if (file) {
        const body = new FormData();
        body.append("file", file);
        const session = getSession();
        const response = await fetch(`${apiRoot()}/communications/${saved.id}/image`, {
          method: "POST",
          headers: session ? { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" } : { Accept: "application/json" },
          body,
        });
        if (!response.ok) {
          throw new ApiError("The circular was saved, but the image was not.", response.status);
        }
      }
      return saved;
    },
    onSuccess: () => onSaved(),
  });

  function update(patch: Partial<Draft>) {
    setDraft((current) => ({ ...current, ...patch }));
  }

  function canNext() {
    if (step === 1) return draft.title.trim().length > 0 && draft.message.trim().length > 0;
    if (step === 2) {
      if (draft.audienceType === "MembershipPackage") return Boolean(draft.audiencePackageId);
      if (draft.audienceType === "TrainerMembers") return Boolean(draft.audienceTrainerId);
      if (draft.audienceType === "SelectedMembers") return draft.selectedMemberIds.length > 0;
      if (draft.audienceType === "SelectedTrainers") return draft.selectedTrainerIds.length > 0;
      if (draft.audienceType === "SelectedStaff") return draft.selectedStaffIds.length > 0;
      return true;
    }
    if (step === 3) return draft.scheduleMode !== "later" || Boolean(draft.publishAt);
    return true;
  }

  return (
    <FormDialog title={`${initial ? "Edit" : "Create"} circular · Step ${step} of 4`} onCancel={onCancel} wide>
      {step === 1 ? (
        <div className="mt-4 grid gap-3">
          <FormField label="Type">
            <select className="field" onChange={(event) => update({ type: event.target.value as CommunicationType })} value={draft.type}>
              {types.map((value) => <option key={value}>{value}</option>)}
            </select>
          </FormField>
          <FormField label="Title"><input className="field" onChange={(event) => update({ title: event.target.value })} value={draft.title} /></FormField>
          <FormField label="Message"><textarea className="field min-h-28 py-2" onChange={(event) => update({ message: event.target.value })} value={draft.message} /></FormField>
          <FormField label="Image (optional)">
            <input accept="image/jpeg,image/png,image/webp" className="field" onChange={(event) => setFile(event.target.files?.[0] ?? null)} type="file" />
          </FormField>
          <FormField label="Priority">
            <select className="field" onChange={(event) => update({ priority: event.target.value as CommunicationPriority })} value={draft.priority}>
              {priorities.map((value) => <option key={value}>{value}</option>)}
            </select>
          </FormField>
        </div>
      ) : null}
      {step === 2 ? (
        <div className="mt-4 grid gap-3">
          <FormField label="Audience">
            <select className="field" onChange={(event) => update({ audienceType: event.target.value as CommunicationAudienceType })} value={draft.audienceType}>
              {audiences.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
          </FormField>
          {draft.audienceType === "ExpiringMembers" ? (
            <FormField label="Expiring within days">
              <input className="field" min={1} max={90} onChange={(event) => update({ expiringWithinDays: event.target.value })} type="number" value={draft.expiringWithinDays} />
            </FormField>
          ) : null}
          {draft.audienceType === "MembershipPackage" ? (
            <FormField label="Membership package">
              <select className="field" onChange={(event) => update({ audiencePackageId: event.target.value })} value={draft.audiencePackageId}>
                <option value="">Select</option>
                {(packages.data?.items ?? []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
              </select>
            </FormField>
          ) : null}
          {draft.audienceType === "TrainerMembers" ? (
            <FormField label="Trainer">
              <select className="field" onChange={(event) => update({ audienceTrainerId: event.target.value })} value={draft.audienceTrainerId}>
                <option value="">Select</option>
                {(trainers.data?.items ?? []).map((item) => <option key={item.id} value={item.id}>{item.firstName} {item.lastName}</option>)}
              </select>
            </FormField>
          ) : null}
          {draft.audienceType === "SelectedMembers" || draft.audienceType === "SelectedTrainers" || draft.audienceType === "SelectedStaff" ? (
            <PersonPicker draft={draft} members={members.data?.items ?? []} staff={staff.data?.items ?? []} trainers={trainers.data?.items ?? []} update={update} />
          ) : null}
        </div>
      ) : null}
      {step === 3 ? (
        <div className="mt-4 grid gap-3">
          <ScheduleFields mode={draft.scheduleMode} onMode={(scheduleMode) => update({ scheduleMode })} onPublishAt={(publishAt) => update({ publishAt })} publishAt={draft.publishAt} timeZone={timeZone} />
          <FormField label="Expires at (optional)"><input className="field" onChange={(event) => update({ expiresAt: event.target.value })} type="datetime-local" value={draft.expiresAt} /></FormField>
        </div>
      ) : null}
      {step === 4 ? (
        <div className="mt-4 grid gap-2 text-sm">
          <p><strong>Type:</strong> {draft.type}</p>
          <p><strong>Title:</strong> {draft.title}</p>
          <p><strong>Message:</strong> {draft.message}</p>
          <p><strong>Priority:</strong> {draft.priority}</p>
          <p><strong>Audience:</strong> {audiences.find((item) => item.value === draft.audienceType)?.label}</p>
          {preview.isLoading ? <p>Calculating recipients…</p> : null}
          {preview.data ? <p>Members: {preview.data.members} · Trainers: {preview.data.trainers} · Staff: {preview.data.staff} · Business users: {preview.data.businessUsers} · Total: {preview.data.total}</p> : null}
          {preview.error instanceof ApiError ? <p role="alert">{preview.error.message}</p> : null}
          <p><strong>Schedule:</strong> {draft.scheduleMode === "now" ? "Publish now" : draft.scheduleMode === "later" ? `Scheduled for ${formatWallClock(draft.publishAt)} (${timeZone})` : "Draft"}</p>
        </div>
      ) : null}
      {create.error instanceof ApiError ? <p className="mt-3" role="alert">{create.error.message}</p> : null}
      <div className="mt-4 flex justify-between gap-3">
        <button className="min-h-11 rounded-full px-4" disabled={step === 1 || create.isPending} onClick={() => setStep((current) => current - 1)} type="button">Back</button>
        {step < 4 ? (
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={!canNext()} onClick={() => setStep((current) => current + 1)} type="button">Next</button>
        ) : (
          <div className="flex gap-3">
            <button className="min-h-11 rounded-full px-4" onClick={onCancel} type="button">Cancel</button>
            <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={create.isPending} onClick={() => create.mutate()} type="button">
              {create.isPending ? "Saving..." : initial ? "Save changes" : draft.scheduleMode === "now" ? "Publish" : draft.scheduleMode === "later" ? "Schedule" : "Save draft"}
            </button>
          </div>
        )}
      </div>
    </FormDialog>
  );
}
