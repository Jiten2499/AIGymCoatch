"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { api, ApiError } from "../../../lib/api";
import type { PlatformTenant } from "../../../lib/types";

export const businessTypes = ["Gym", "YogaStudio", "CrossFit", "Physio", "Corporate"] as const;
const statuses = ["Active", "Suspended", "Inactive"] as const;

const createSchema = z.object({
  name: z.string().min(1, "Enter a business name").max(200),
  tenantType: z.enum(businessTypes),
  ownerEmail: z.string().email("Enter the owner's email"),
  ownerFirstName: z.string().min(1, "Enter a first name").max(100),
  ownerLastName: z.string().min(1, "Enter a last name").max(100),
});

const editSchema = z.object({
  name: z.string().min(1, "Enter a name").max(200),
  tenantType: z.enum([...businessTypes, "Platform"]),
  status: z.enum(statuses),
});

export type CreatedTenant = {
  id: string;
  name: string;
  ownerEmail: string;
  temporaryPassword: string | null;
};

type CreateValues = z.infer<typeof createSchema>;
type EditValues = z.infer<typeof editSchema>;

export function AddTenantDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: (tenant: CreatedTenant) => void }) {
  const form = useForm<CreateValues>({ resolver: zodResolver(createSchema), defaultValues: { name: "", tenantType: "Gym", ownerEmail: "", ownerFirstName: "", ownerLastName: "" } });
  const create = useMutation({
    mutationFn: (values: CreateValues) => api<CreatedTenant>("/platform/tenants", { method: "POST", body: JSON.stringify(values) }),
    onSuccess: (tenant) => onCreated(tenant),
  });

  return (
    <Dialog title="Add business tenant" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => create.mutate(values))}>
        <Field label="Business name" error={form.formState.errors.name?.message}>
          <input className="field" {...form.register("name")} />
        </Field>
        <Field label="Type">
          <select aria-label="Type" className="field" {...form.register("tenantType")}>
            {businessTypes.map((type) => <option key={type}>{type}</option>)}
          </select>
        </Field>
        <Field label="Owner email" error={form.formState.errors.ownerEmail?.message}>
          <input className="field" type="email" {...form.register("ownerEmail")} />
        </Field>
        <Field label="Owner first name" error={form.formState.errors.ownerFirstName?.message}>
          <input className="field" {...form.register("ownerFirstName")} />
        </Field>
        <Field label="Owner last name" error={form.formState.errors.ownerLastName?.message}>
          <input className="field" {...form.register("ownerLastName")} />
        </Field>
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <Actions cancel={onCancel} pending={create.isPending} submitLabel="Create tenant" />
      </form>
    </Dialog>
  );
}

export function EditTenantDialog({ tenant, onCancel, onSaved }: { tenant: PlatformTenant; onCancel: () => void; onSaved: () => void }) {
  const platform = tenant.tenantType === "Platform";
  const form = useForm<EditValues>({
    resolver: zodResolver(editSchema),
    defaultValues: { name: tenant.name, tenantType: tenant.tenantType as EditValues["tenantType"], status: tenant.status as EditValues["status"] },
  });
  const save = useMutation({
    mutationFn: (values: EditValues) => api(`/platform/tenants/${tenant.id}`, { method: "PUT", body: JSON.stringify(values) }),
    onSuccess: () => onSaved(),
  });

  return (
    <Dialog title="Edit tenant" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => save.mutate(values))}>
        <Field label="Name" error={form.formState.errors.name?.message}>
          <input className="field" {...form.register("name")} />
        </Field>
        <Field label="Type">
          <select aria-label="Type" className="field" disabled={platform} {...form.register("tenantType")}>
            {(platform ? ["Platform"] : businessTypes).map((type) => <option key={type}>{type}</option>)}
          </select>
        </Field>
        <Field label="Status">
          <select aria-label="Status" className="field" {...form.register("status")}>
            {statuses.map((status) => <option key={status}>{status}</option>)}
          </select>
        </Field>
        {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
        <Actions cancel={onCancel} pending={save.isPending} submitLabel="Save" />
      </form>
    </Dialog>
  );
}

export function PasswordDialog({ created, onClose }: { created: CreatedTenant; onClose: () => void }) {
  const [copied, setCopied] = useState(false);
  const password = created.temporaryPassword;

  return (
    <Dialog title="Tenant owner sign-in" onCancel={onClose}>
      <p className="mt-3 text-muted">Email delivery is not available yet. This password is shown only now. Give it to the owner of {created.name}.</p>
      <p className="mt-4">Email: {created.ownerEmail}</p>
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

function Dialog({ title, onCancel, children }: { title: string; onCancel: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation">
      <div aria-labelledby="tenant-dialog-title" aria-modal="true" className="w-full max-w-lg rounded-2xl border border-line bg-surface p-6" role="dialog">
        <h2 className="text-xl font-semibold" id="tenant-dialog-title">{title}</h2>
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
