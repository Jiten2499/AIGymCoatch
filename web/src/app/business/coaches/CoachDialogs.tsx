"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { api, apiBlob, ApiError } from "../../../lib/api";
import type { Coach, CreatedCoach, Page } from "../../../lib/types";
import { CoachPhotoField, coachPhotoError, uploadCoachPhoto } from "./CoachPhotoField";

const years = z.string().trim().refine((value) => value === "" || (/^\d+$/.test(value) && Number(value) <= 80), "Enter 0 to 80");
const phone = z.string().trim().max(32).refine((value) => value === "" || /^\+?[0-9][0-9 \-]{6,30}$/.test(value), "Enter a valid phone number");

const createSchema = z.object({
  email: z.string().email("Enter an email"),
  firstName: z.string().trim().min(1, "Enter a first name").max(100),
  lastName: z.string().trim().min(1, "Enter a last name").max(100),
  phone,
  specialization: z.string().trim().max(200),
  branchId: z.string(),
});

const editSchema = z.object({
  firstName: z.string().trim().min(1, "Enter a first name").max(100),
  lastName: z.string().trim().min(1, "Enter a last name").max(100),
  phone,
  specialization: z.string().trim().max(200),
  experienceYears: years,
  certification: z.string().trim().max(500),
  bio: z.string().trim().max(2000),
  employeeCode: z.string().trim().max(32),
  joiningDate: z.string(),
  commissionEligible: z.boolean(),
  branchId: z.string(),
  status: z.enum(["Active", "Inactive"]),
});

type CreateValues = z.infer<typeof createSchema>;
type EditValues = z.infer<typeof editSchema>;
type BranchRow = { id: string; name: string; status: string };

function useBranches() {
  return useQuery({
    queryKey: ["branches"],
    retry: false,
    queryFn: () => api<Page<BranchRow>>("/branches?page=1&pageSize=100"),
  });
}

export function AddCoachDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: (coach: CreatedCoach, photoWarning?: string) => void }) {
  const branches = useBranches();
  const [photo, setPhoto] = useState<File | null>(null);
  const [photoMessage, setPhotoMessage] = useState<string | null>(null);
  const [sendActivationLink, setSendActivationLink] = useState(false);
  const active = (branches.data?.items ?? []).filter((branch) => branch.status === "Active");
  const form = useForm<CreateValues>({ resolver: zodResolver(createSchema), defaultValues: { email: "", firstName: "", lastName: "", phone: "", specialization: "", branchId: "" } });
  const watched = form.watch(["firstName", "lastName"]);
  const create = useMutation({
    mutationFn: async (values: CreateValues) => {
      const picture = coachPhotoError(photo);
      if (picture) {
        throw new ApiError(picture, 400);
      }
      const coach = await api<CreatedCoach>("/trainers", {
        method: "POST",
        body: JSON.stringify({
          email: values.email,
          firstName: values.firstName,
          lastName: values.lastName,
          phone: values.phone || null,
          specialization: values.specialization || null,
          branchId: values.branchId || null,
          sendActivationLink,
        }),
      });
      if (photo) {
        try {
          await uploadCoachPhoto(coach.id, photo);
        } catch (error) {
          return { coach, photoWarning: error instanceof ApiError ? error.message : "The coach was created, but the photo was not." };
        }
      }
      return { coach, photoWarning: undefined };
    },
    onSuccess: ({ coach, photoWarning }) => onCreated(coach, photoWarning),
  });

  return (
    <Dialog title="Add coach" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => create.mutate(values))}>
        <CoachPhotoField existing={null} file={photo} firstName={watched[0]} lastName={watched[1]} onFile={(file, message) => { setPhoto(file); setPhotoMessage(message); }} onRemove={() => { setPhoto(null); setPhotoMessage(null); }} removed={!photo} />
        <Field label="Email" error={form.formState.errors.email?.message}>
          <input className="field" type="email" {...form.register("email")} />
        </Field>
        <Field label="First name" error={form.formState.errors.firstName?.message}>
          <input className="field" {...form.register("firstName")} />
        </Field>
        <Field label="Last name" error={form.formState.errors.lastName?.message}>
          <input className="field" {...form.register("lastName")} />
        </Field>
        <Field label="Phone" error={form.formState.errors.phone?.message}>
          <input className="field" {...form.register("phone")} />
        </Field>
        <Field label="Specialization" error={form.formState.errors.specialization?.message}>
          <input className="field" {...form.register("specialization")} />
        </Field>
        {active.length > 0 ? <BranchField branches={active} register={form.register("branchId")} /> : null}
        <label className="flex min-h-11 items-center gap-3 text-sm">
          <input checked={sendActivationLink} className="size-4" onChange={(event) => setSendActivationLink(event.target.checked)} type="checkbox" />
          Send activation link
        </label>
        {photoMessage ? <p role="alert">{photoMessage}</p> : null}
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <Actions cancel={onCancel} pending={create.isPending} submitLabel="Create coach" />
      </form>
    </Dialog>
  );
}

export function EditCoachDialog({ coach, onCancel, onSaved }: { coach: Coach; onCancel: () => void; onSaved: () => void }) {
  const branches = useBranches();
  const [photo, setPhoto] = useState<File | null>(null);
  const [removePhoto, setRemovePhoto] = useState(false);
  const [photoMessage, setPhotoMessage] = useState<string | null>(null);
  const active = (branches.data?.items ?? []).filter((branch) => branch.status === "Active");
  const existing = useQuery({ queryKey: ["coach-photo", coach.id], enabled: coach.hasPhoto === true && !photo && !removePhoto, retry: false, queryFn: () => apiBlob(`/trainers/${coach.id}/profile-photo`) });
  const form = useForm<EditValues>({
    resolver: zodResolver(editSchema),
    defaultValues: {
      firstName: coach.firstName,
      lastName: coach.lastName,
      phone: coach.phone ?? "",
      specialization: coach.specialization ?? "",
      experienceYears: coach.experienceYears === null || coach.experienceYears === undefined ? "" : String(coach.experienceYears),
      certification: coach.certification ?? "",
      bio: coach.bio ?? "",
      employeeCode: coach.employeeCode ?? "",
      joiningDate: coach.joiningDate ?? "",
      commissionEligible: coach.commissionEligible === true,
      branchId: coach.branchId ?? "",
      status: coach.status === "Inactive" ? "Inactive" : "Active",
    },
  });
  const watched = form.watch(["firstName", "lastName"]);
  const save = useMutation({
    mutationFn: async (values: EditValues) => {
      const picture = coachPhotoError(photo);
      if (picture) {
        throw new ApiError(picture, 400);
      }
      await api(`/trainers/${coach.id}`, {
        method: "PUT",
        body: JSON.stringify({
          firstName: values.firstName,
          lastName: values.lastName,
          phone: values.phone,
          setPhone: true,
          bio: values.bio || null,
          specialization: values.specialization || null,
          experienceYears: values.experienceYears === "" ? null : Number(values.experienceYears),
          certification: values.certification || null,
          profileImageUrl: coach.profileImageUrl,
          employeeCode: values.employeeCode,
          joiningDate: values.joiningDate || null,
          commissionEligible: values.commissionEligible,
          setEmployment: true,
          branchId: values.branchId || null,
          status: values.status,
        }),
      });
      if (photo) {
        await uploadCoachPhoto(coach.id, photo);
      } else if (removePhoto && coach.hasPhoto) {
        await api(`/trainers/${coach.id}/profile-photo`, { method: "DELETE" });
      }
    },
    onSuccess: () => onSaved(),
  });

  return (
    <Dialog title="Edit coach" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => save.mutate(values))}>
        <CoachPhotoField existing={existing.data ?? null} file={photo} firstName={watched[0]} lastName={watched[1]} onFile={(file, message) => { setRemovePhoto(false); setPhoto(file); setPhotoMessage(message); }} onRemove={() => { setPhoto(null); setRemovePhoto(true); setPhotoMessage(null); }} removed={removePhoto} />
        <p>Email: {coach.email}</p>
        <Field label="First name" error={form.formState.errors.firstName?.message}><input className="field" {...form.register("firstName")} /></Field>
        <Field label="Last name" error={form.formState.errors.lastName?.message}><input className="field" {...form.register("lastName")} /></Field>
        <Field label="Phone" error={form.formState.errors.phone?.message}><input className="field" {...form.register("phone")} /></Field>
        <Field label="Specialization" error={form.formState.errors.specialization?.message}><input className="field" {...form.register("specialization")} /></Field>
        <Field label="Experience (years)" error={form.formState.errors.experienceYears?.message}><input className="field" inputMode="numeric" {...form.register("experienceYears")} /></Field>
        <Field label="Certification" error={form.formState.errors.certification?.message}><input className="field" {...form.register("certification")} /></Field>
        <Field label="Bio" error={form.formState.errors.bio?.message}><textarea className="field min-h-24" {...form.register("bio")} /></Field>
        <Field label="Employee code" error={form.formState.errors.employeeCode?.message}><input className="field" {...form.register("employeeCode")} /></Field>
        <Field label="Joining date" error={form.formState.errors.joiningDate?.message}><input className="field" type="date" {...form.register("joiningDate")} /></Field>
        <label className="flex min-h-11 items-center gap-2"><input type="checkbox" {...form.register("commissionEligible")} /> Eligible for commission</label>
        {active.length > 0 ? <BranchField branches={active} register={form.register("branchId")} /> : null}
        <Field label="Status">
          <select aria-label="Status" className="field" {...form.register("status")}><option>Active</option><option>Inactive</option></select>
        </Field>
        {photoMessage ? <p role="alert">{photoMessage}</p> : null}
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <Actions cancel={onCancel} pending={save.isPending} submitLabel="Save coach" />
      </form>
    </Dialog>
  );
}

export function PasswordDialog({ created, notice, onClose }: { created: CreatedCoach; notice?: string | null; onClose: () => void }) {
  const password = created.temporaryPassword;
  const [copied, setCopied] = useState(false);
  return (
    <Dialog title="Coach account" onCancel={onClose}>
      <p className="mt-3">{created.firstName} {created.lastName} can sign in as {created.email}.</p>
      {notice ? <p className="mt-3" role="alert">{notice}</p> : null}
      {password ? (
        <label className="mt-4 grid gap-2">
          Password
          <input aria-label="Temporary password" className="field font-mono" readOnly value={password} />
        </label>
      ) : (
        <p className="mt-3">This email already has an account, so no new password was created.</p>
      )}
      <div className="mt-6 flex justify-end gap-3">
        {password ? (
          <button className="min-h-11 rounded-full px-4" onClick={() => { void navigator.clipboard.writeText(password).then(() => setCopied(true)); }} type="button">
            {copied ? "Copied" : "Copy password"}
          </button>
        ) : null}
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={onClose} type="button">Close</button>
      </div>
    </Dialog>
  );
}

function BranchField({ branches, register }: { branches: BranchRow[]; register: ReturnType<ReturnType<typeof useForm<CreateValues>>["register"]> }) {
  return (
    <Field label="Branch">
      <select aria-label="Branch" className="field" {...register}>
        <option value="">No branch</option>
        {branches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
      </select>
    </Field>
  );
}

function Dialog({ title, onCancel, children }: { title: string; onCancel: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation">
      <div aria-labelledby="coach-dialog-title" aria-modal="true" className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl border border-line bg-surface p-6" role="dialog">
        <div className="flex items-start justify-between gap-4">
          <h2 className="text-xl font-semibold" id="coach-dialog-title">{title}</h2>
          <button aria-label="Close" className="flex size-11 shrink-0 items-center justify-center rounded-full border border-line text-lg" onClick={onCancel} type="button">×</button>
        </div>
        {children}
      </div>
    </div>
  );
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-2">
      {label}
      {children}
      {error ? <span className="text-sm text-red-300">{error}</span> : null}
    </label>
  );
}

function Actions({ cancel, pending, submitLabel }: { cancel: () => void; pending: boolean; submitLabel: string }) {
  return (
    <div className="mt-2 flex justify-end gap-3">
      <button className="min-h-11 rounded-full px-4" onClick={cancel} type="button">Cancel</button>
      <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={pending} type="submit">
        {pending ? "Saving..." : submitLabel}
      </button>
    </div>
  );
}
