"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { api, ApiError } from "../../../lib/api";
import type { Coach, CreatedCoach, Page } from "../../../lib/types";

const years = z.string().trim().refine((value) => value === "" || (/^\d+$/.test(value) && Number(value) <= 80), "Enter 0 to 80");

const createSchema = z.object({
  email: z.string().email("Enter an email"),
  firstName: z.string().trim().min(1, "Enter a first name").max(100),
  lastName: z.string().trim().min(1, "Enter a last name").max(100),
  phone: z.string().trim().max(32),
  specialization: z.string().trim().max(200),
  branchId: z.string(),
});

const editSchema = z.object({
  firstName: z.string().trim().min(1, "Enter a first name").max(100),
  lastName: z.string().trim().min(1, "Enter a last name").max(100),
  specialization: z.string().trim().max(200),
  experienceYears: years,
  certification: z.string().trim().max(500),
  bio: z.string().trim().max(2000),
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

export function AddCoachDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: (coach: CreatedCoach) => void }) {
  const branches = useBranches();
  const active = (branches.data?.items ?? []).filter((branch) => branch.status === "Active");
  const form = useForm<CreateValues>({ resolver: zodResolver(createSchema), defaultValues: { email: "", firstName: "", lastName: "", phone: "", specialization: "", branchId: "" } });
  const create = useMutation({
    mutationFn: (values: CreateValues) => api<CreatedCoach>("/trainers", {
      method: "POST",
      body: JSON.stringify({
        email: values.email,
        firstName: values.firstName,
        lastName: values.lastName,
        phone: values.phone || null,
        specialization: values.specialization || null,
        branchId: values.branchId || null,
      }),
    }),
    onSuccess: (coach) => onCreated(coach),
  });

  return (
    <Dialog title="Add coach" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => create.mutate(values))}>
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
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <Actions cancel={onCancel} pending={create.isPending} submitLabel="Create coach" />
      </form>
    </Dialog>
  );
}

export function EditCoachDialog({ coach, onCancel, onSaved }: { coach: Coach; onCancel: () => void; onSaved: () => void }) {
  const branches = useBranches();
  const active = (branches.data?.items ?? []).filter((branch) => branch.status === "Active");
  const form = useForm<EditValues>({
    resolver: zodResolver(editSchema),
    defaultValues: {
      firstName: coach.firstName,
      lastName: coach.lastName,
      specialization: coach.specialization ?? "",
      experienceYears: coach.experienceYears === null ? "" : String(coach.experienceYears),
      certification: coach.certification ?? "",
      bio: coach.bio ?? "",
      branchId: coach.branchId ?? "",
      status: coach.status === "Inactive" ? "Inactive" : "Active",
    },
  });
  const save = useMutation({
    mutationFn: (values: EditValues) => api(`/trainers/${coach.id}`, {
      method: "PUT",
      body: JSON.stringify({
        firstName: values.firstName,
        lastName: values.lastName,
        bio: values.bio || null,
        specialization: values.specialization || null,
        experienceYears: values.experienceYears === "" ? null : Number(values.experienceYears),
        certification: values.certification || null,
        profileImageUrl: coach.profileImageUrl,
        branchId: values.branchId || null,
        status: values.status,
      }),
    }),
    onSuccess: () => onSaved(),
  });

  return (
    <Dialog title="Edit coach" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => save.mutate(values))}>
        <p>Email: {coach.email}</p>
        <Field label="First name" error={form.formState.errors.firstName?.message}>
          <input className="field" {...form.register("firstName")} />
        </Field>
        <Field label="Last name" error={form.formState.errors.lastName?.message}>
          <input className="field" {...form.register("lastName")} />
        </Field>
        <Field label="Specialization" error={form.formState.errors.specialization?.message}>
          <input className="field" {...form.register("specialization")} />
        </Field>
        <Field label="Experience (years)" error={form.formState.errors.experienceYears?.message}>
          <input className="field" inputMode="numeric" {...form.register("experienceYears")} />
        </Field>
        <Field label="Certification" error={form.formState.errors.certification?.message}>
          <input className="field" {...form.register("certification")} />
        </Field>
        <Field label="Bio" error={form.formState.errors.bio?.message}>
          <textarea className="field min-h-24" {...form.register("bio")} />
        </Field>
        {active.length > 0 ? <BranchField branches={active} register={form.register("branchId")} /> : null}
        <Field label="Status">
          <select aria-label="Status" className="field" {...form.register("status")}>
            <option>Active</option>
            <option>Inactive</option>
          </select>
        </Field>
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <Actions cancel={onCancel} pending={save.isPending} submitLabel="Save coach" />
      </form>
    </Dialog>
  );
}

export function PasswordDialog({ created, onClose }: { created: CreatedCoach; onClose: () => void }) {
  const password = created.temporaryPassword;
  const [copied, setCopied] = useState(false);
  return (
    <Dialog title="Coach account" onCancel={onClose}>
      <p className="mt-3">{created.firstName} {created.lastName} can sign in as {created.email}.</p>
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
      <div aria-labelledby="coach-dialog-title" aria-modal="true" className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-line bg-surface p-6" role="dialog">
        <h2 className="text-xl font-semibold" id="coach-dialog-title">{title}</h2>
        {children}
        <button className="sr-only" onClick={onCancel} type="button">Close</button>
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
