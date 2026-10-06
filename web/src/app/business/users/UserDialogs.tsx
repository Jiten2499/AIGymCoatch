"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { api, ApiError } from "../../../lib/api";
import type { CreatedWorkspaceUser, WorkspacePlan, WorkspaceUser } from "../../../lib/types";

export const workspaceRoles = ["GymOwner", "GymManager", "Trainer", "Receptionist", "Member"] as const;
const statuses = ["Active", "Disabled"] as const;

const createSchema = z.object({
  email: z.string().email("Enter an email"),
  firstName: z.string().min(1, "Enter a first name").max(100),
  lastName: z.string().min(1, "Enter a last name").max(100),
  roleName: z.enum(workspaceRoles),
});

const editSchema = z.object({
  firstName: z.string().min(1, "Enter a first name").max(100),
  lastName: z.string().min(1, "Enter a last name").max(100),
  status: z.enum(statuses),
  roleName: z.enum(workspaceRoles),
});

type CreateValues = z.infer<typeof createSchema>;
type EditValues = z.infer<typeof editSchema>;

export function AddUserDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: (user: CreatedWorkspaceUser) => void }) {
  const form = useForm<CreateValues>({ resolver: zodResolver(createSchema), defaultValues: { email: "", firstName: "", lastName: "", roleName: "Member" } });
  const create = useMutation({
    mutationFn: (values: CreateValues) => api<CreatedWorkspaceUser>("/business/users", { method: "POST", body: JSON.stringify(values) }),
    onSuccess: (user) => onCreated(user),
  });

  return (
    <Dialog title="Add user" onCancel={onCancel}>
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
        <RoleField register={form.register("roleName")} />
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <Actions cancel={onCancel} pending={create.isPending} submitLabel="Create user" />
      </form>
    </Dialog>
  );
}

export function EditUserDialog({ user, onCancel, onSaved }: { user: WorkspaceUser; onCancel: () => void; onSaved: () => void }) {
  const roleName = workspaceRoles.includes(user.role as EditValues["roleName"]) ? (user.role as EditValues["roleName"]) : "Member";
  const status = user.status === "Disabled" ? "Disabled" : "Active";
  const form = useForm<EditValues>({ resolver: zodResolver(editSchema), defaultValues: { firstName: user.firstName, lastName: user.lastName, status, roleName } });
  const save = useMutation({
    mutationFn: (values: EditValues) => api(`/business/users/${user.id}`, { method: "PUT", body: JSON.stringify(values) }),
    onSuccess: () => onSaved(),
  });

  return (
    <Dialog title="Edit user" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => save.mutate(values))}>
        <p>Email: {user.email}</p>
        <Field label="First name" error={form.formState.errors.firstName?.message}>
          <input className="field" {...form.register("firstName")} />
        </Field>
        <Field label="Last name" error={form.formState.errors.lastName?.message}>
          <input className="field" {...form.register("lastName")} />
        </Field>
        <Field label="Status">
          <select aria-label="Status" className="field" disabled={user.isOwner} {...form.register("status")}>
            {statuses.map((item) => <option key={item}>{item}</option>)}
          </select>
        </Field>
        <RoleField disabled={user.isOwner} register={form.register("roleName")} />
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <Actions cancel={onCancel} pending={save.isPending} submitLabel="Save" />
      </form>
    </Dialog>
  );
}

export function PasswordDialog({ created, onClose }: { created: CreatedWorkspaceUser; onClose: () => void }) {
  const [copied, setCopied] = useState(false);
  const password = created.temporaryPassword;

  return (
    <Dialog title="User sign-in" onCancel={onClose}>
      <p className="mt-3 text-muted">Email delivery is not available yet. This password is shown only now. Give it to {created.firstName} {created.lastName}.</p>
      <p className="mt-4">Email: {created.email}</p>
      {password ? (
        <label className="mt-3 grid gap-2">
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

export function AssignPlanDialog({ user, onCancel, onSaved }: { user: WorkspaceUser; onCancel: () => void; onSaved: () => void }) {
  const plans = useQuery({ queryKey: ["workspace-plans"], queryFn: () => api<WorkspacePlan[]>("/business/plans") });
  const choices = (plans.data ?? []).filter((plan) => plan.usable && (plan.available > 0 || plan.planName === user.plan));
  const [picked, setPicked] = useState<string | null>(null);
  const subscriptionId = picked ?? choices.find((plan) => plan.planName === user.plan)?.subscriptionId ?? choices[0]?.subscriptionId ?? "";
  const assign = useMutation({
    mutationFn: () => api(`/business/users/${user.id}/plan`, { method: "POST", body: JSON.stringify({ subscriptionId }) }),
    onSuccess: () => onSaved(),
  });

  return (
    <Dialog title="Assign plan" onCancel={onCancel}>
      <p className="mt-3 text-muted">Choose one of the plans assigned to this workspace for {user.firstName} {user.lastName}.</p>
      {plans.isLoading ? <p className="mt-4">Loading plans.</p> : null}
      {plans.error instanceof ApiError ? <p className="mt-4" role="alert">{plans.error.message}</p> : null}
      {!plans.isLoading && choices.length === 0 ? <p className="mt-4">No seats are available on the plans assigned to this workspace.</p> : null}
      {choices.length > 0 ? (
        <form className="mt-4 grid gap-3" onSubmit={(event) => { event.preventDefault(); assign.mutate(); }}>
          <Field label="Plan">
            <select aria-label="Plan" className="field" onChange={(event) => setPicked(event.target.value)} value={subscriptionId}>
              {choices.map((plan) => <option key={plan.subscriptionId} value={plan.subscriptionId}>{plan.planName} · {plan.available} seats available</option>)}
            </select>
          </Field>
          {assign.error instanceof ApiError ? <p role="alert">{assign.error.message}</p> : null}
          <Actions cancel={onCancel} pending={assign.isPending} submitLabel="Assign plan" />
        </form>
      ) : (
        <div className="mt-6 flex justify-end">
          <button className="min-h-11 rounded-full px-4" onClick={onCancel} type="button">Close</button>
        </div>
      )}
    </Dialog>
  );
}

function RoleField({ register, disabled }: { register: ReturnType<ReturnType<typeof useForm<CreateValues>>["register"]>; disabled?: boolean }) {
  return (
    <Field label="Role">
      <select aria-label="Role" className="field" disabled={disabled} {...register}>
        {workspaceRoles.map((role) => <option key={role}>{role}</option>)}
      </select>
    </Field>
  );
}

function Dialog({ title, onCancel, children }: { title: string; onCancel: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation">
      <div aria-labelledby="user-dialog-title" aria-modal="true" className="w-full max-w-lg rounded-2xl border border-line bg-surface p-6" role="dialog">
        <h2 className="text-xl font-semibold" id="user-dialog-title">{title}</h2>
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
