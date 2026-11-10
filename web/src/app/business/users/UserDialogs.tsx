"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useMemo, useState, useSyncExternalStore } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { api, ApiError } from "../../../lib/api";
import { getSession, subscribeSession } from "../../../lib/session";
import type { CreatedWorkspaceUser, WorkspacePlan, WorkspaceUser } from "../../../lib/types";
import { StaffPhotoField, uploadStaffPhoto } from "./StaffPhotoField";

export const staffRoleOptions = [
  { name: "GymOwner", label: "Business Owner", custom: false },
  { name: "BusinessAdmin", label: "Business Admin", custom: false },
  { name: "GymManager", label: "Manager", custom: false },
  { name: "Receptionist", label: "Receptionist", custom: false },
  { name: "Accountant", label: "Accountant", custom: false },
] as const;

export type StaffRoleOption = { name: string; label: string; custom: boolean };

export function staffRoleLabel(roleName: string) {
  return staffRoleOptions.find((role) => role.name === roleName)?.label ?? roleName;
}

const statuses = ["Active", "Disabled"] as const;

const createSchema = z.object({
  email: z.string().email("Enter an email"),
  firstName: z.string().min(1, "Enter a first name").max(100),
  lastName: z.string().min(1, "Enter a last name").max(100),
  roleName: z.string().min(1, "Choose a role"),
});

const editSchema = z.object({
  firstName: z.string().min(1, "Enter a first name").max(100),
  lastName: z.string().min(1, "Enter a last name").max(100),
  status: z.enum(statuses),
  roleName: z.string().min(1, "Choose a role"),
});

type CreateValues = z.infer<typeof createSchema>;
type EditValues = z.infer<typeof editSchema>;

export function useStaffRoles(currentRole?: string) {
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  const catalog = useQuery({
    queryKey: ["staff-roles", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<StaffRoleOption[]>("/business/users/roles"),
    staleTime: 30_000,
  });

  const roles = useMemo(() => {
    const loaded = catalog.data?.map((role) => ({ name: role.name, label: role.label || staffRoleLabel(role.name), custom: role.custom }));
    const source = loaded && loaded.length > 0
      ? loaded
      : catalog.isError
        ? staffRoleOptions.map((role) => ({ name: role.name, label: role.label, custom: false }))
        : [];
    if (currentRole && currentRole !== "Trainer" && source.length > 0 && !source.some((role) => role.name === currentRole)) {
      return [...source, { name: currentRole, label: staffRoleLabel(currentRole), custom: true }];
    }
    return source;
  }, [catalog.data, catalog.isError, currentRole]);

  return { roles, loading: catalog.isLoading, failed: catalog.isError };
}

export function AddUserDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: (user: CreatedWorkspaceUser, photoWarning?: string) => void }) {
  const { roles, loading, failed } = useStaffRoles();
  const form = useForm<CreateValues>({ resolver: zodResolver(createSchema), defaultValues: { email: "", firstName: "", lastName: "", roleName: "Receptionist" } });
  const [photo, setPhoto] = useState<File | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const names = form.watch(["firstName", "lastName"]);
  const create = useMutation({
    mutationFn: async (values: CreateValues) => {
      const user = await api<CreatedWorkspaceUser>("/business/users", { method: "POST", body: JSON.stringify(values) });
      if (!photo) {
        return { user };
      }

      try {
        await uploadStaffPhoto(user.id, photo);
        return { user };
      } catch (error) {
        return { user, photoWarning: error instanceof ApiError ? error.message : "The staff member was created, but the photo was not." };
      }
    },
    onSuccess: (result) => onCreated(result.user, result.photoWarning),
  });

  return (
    <Dialog title="Add staff" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => { if (!photoError) create.mutate(values); })}>
        <StaffPhotoField firstName={names[0]} lastName={names[1]} onFile={(file, message) => { setPhoto(file); setPhotoError(message); }} />
        {photoError ? <p role="alert">{photoError}</p> : null}
        <Field label="Email" error={form.formState.errors.email?.message}>
          <input className="field" type="email" {...form.register("email")} />
        </Field>
        <Field label="First name" error={form.formState.errors.firstName?.message}>
          <input className="field" {...form.register("firstName")} />
        </Field>
        <Field label="Last name" error={form.formState.errors.lastName?.message}>
          <input className="field" {...form.register("lastName")} />
        </Field>
        <RoleField error={form.formState.errors.roleName?.message} failed={failed} loading={loading} register={form.register("roleName")} roles={roles} />
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <Actions cancel={onCancel} pending={create.isPending} submitLabel="Create staff" />
      </form>
    </Dialog>
  );
}

export function EditUserDialog({ user, onCancel, onSaved }: { user: WorkspaceUser; onCancel: () => void; onSaved: () => void }) {
  const { roles, loading, failed } = useStaffRoles(user.role);
  const roleName = user.role === "Trainer" ? "Receptionist" : user.role || "Receptionist";
  const status = user.status === "Disabled" ? "Disabled" : "Active";
  const form = useForm<EditValues>({ resolver: zodResolver(editSchema), defaultValues: { firstName: user.firstName, lastName: user.lastName, status, roleName } });
  const save = useMutation({
    mutationFn: (values: EditValues) => api(`/business/users/${user.id}`, { method: "PUT", body: JSON.stringify(values) }),
    onSuccess: () => onSaved(),
  });

  return (
    <Dialog title="Edit staff" onCancel={onCancel}>
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
        <RoleField disabled={user.isOwner} error={form.formState.errors.roleName?.message} failed={failed} loading={loading} register={form.register("roleName")} roles={roles} />
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <Actions cancel={onCancel} pending={save.isPending} submitLabel="Save" />
      </form>
    </Dialog>
  );
}

export function PasswordDialog({ created, notice, onClose }: { created: CreatedWorkspaceUser; notice?: string | null; onClose: () => void }) {
  const [copied, setCopied] = useState(false);
  const password = created.temporaryPassword;

  return (
    <Dialog title="User sign-in" onCancel={onClose}>
      <p className="mt-3 text-muted">Email delivery is not available yet. This password is shown only now. Give it to {created.firstName} {created.lastName}.</p>
      {notice ? <p className="mt-3" role="alert">{notice}</p> : null}
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

export function InviteResultDialog({
  created,
  notice,
  compensationHref,
  activationQueued = false,
  onClose,
}: {
  created: CreatedWorkspaceUser;
  notice?: string | null;
  compensationHref?: string | null;
  activationQueued?: boolean;
  onClose: () => void;
}) {
  const draft = created.status === "Draft";

  return (
    <Dialog title={draft ? "Staff draft saved" : "Staff invitation"} onCancel={onClose}>
      {notice ? <p className="mt-3" role="alert">{notice}</p> : null}
      <p className="mt-3 text-muted">
        {draft
          ? `${created.firstName} ${created.lastName} was saved as a draft. No invitation was sent and no seat was consumed.`
            : activationQueued
            ? `An activation link was queued for ${created.firstName} ${created.lastName}. New accounts use that link to set a password. Existing LifeFit AI accounts receive a business invitation and keep their password.`
            : `${created.firstName} ${created.lastName} was created. No activation email was sent.`}
      </p>
      <p className="mt-4">Email: {created.email}</p>
      {created.employeeCode ? <p className="mt-2">Employee ID: {created.employeeCode}</p> : null}
      {created.status ? <p className="mt-2">Status: {created.status}</p> : null}
      <div className="mt-6 flex flex-wrap justify-end gap-3">
        {compensationHref ? (
          <a className="inline-flex min-h-11 items-center rounded-full border border-line px-4" href={compensationHref}>Set up compensation</a>
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

function RoleField({
  register,
  roles,
  disabled,
  error,
  loading,
  failed,
}: {
  register: ReturnType<ReturnType<typeof useForm<CreateValues>>["register"]>;
  roles: StaffRoleOption[];
  disabled?: boolean;
  error?: string;
  loading?: boolean;
  failed?: boolean;
}) {
  return (
    <Field label="Role" error={error}>
      {loading ? <p>Loading roles…</p> : null}
      {failed ? <p role="alert">Custom roles could not be loaded. The standard roles are shown below.</p> : null}
      <div className="max-h-56 overflow-y-auto overflow-x-hidden overscroll-contain rounded-xl border border-line">
        <select aria-label="Role" className="field h-auto max-h-56 w-full overflow-y-auto border-0 py-1" disabled={disabled} size={Math.min(Math.max(roles.length, 1), 8)} {...register}>
          {roles.map((role) => <option key={role.name} value={role.name}>{role.custom ? `${role.label} (custom)` : role.label}</option>)}
        </select>
      </div>
    </Field>
  );
}

function Dialog({ title, onCancel, children }: { title: string; onCancel: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation">
      <div aria-labelledby="user-dialog-title" aria-modal="true" className="max-h-[calc(100vh-2rem)] w-full max-w-lg overflow-y-auto overscroll-contain rounded-2xl border border-line bg-surface p-6" role="dialog">
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
