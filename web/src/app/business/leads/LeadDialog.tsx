"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { emptyToNull, type LeadDetails } from "../../../lib/gym";

const sources = ["WalkIn", "Phone", "Instagram", "Facebook", "Google", "Website", "Referral", "Advertisement", "Other"] as const;

const schema = z.object({
  fullName: z.string().min(1, "Enter a name").max(200),
  mobile: z.string().min(7, "Enter a mobile number").max(32),
  email: z.string().max(200),
  source: z.enum(sources),
  notes: z.string().max(2000),
});

type Values = z.infer<typeof schema>;

export function LeadDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: () => void }) {
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { fullName: "", mobile: "", email: "", source: "WalkIn", notes: "" } });
  const create = useMutation({
    mutationFn: (values: Values) => api<LeadDetails>("/leads", {
      method: "POST",
      body: JSON.stringify({
        fullName: values.fullName,
        mobile: values.mobile,
        email: emptyToNull(values.email),
        source: values.source,
        interestedPlanId: null,
        assignedUserId: null,
        nextFollowUpAt: null,
        notes: emptyToNull(values.notes),
      }),
    }),
    onSuccess: () => onCreated(),
  });

  return (
    <FormDialog title="Add lead" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => create.mutate(values))}>
        <FormField label="Name" error={form.formState.errors.fullName?.message}><input className="field" {...form.register("fullName")} /></FormField>
        <FormField label="Mobile" error={form.formState.errors.mobile?.message}><input className="field" {...form.register("mobile")} /></FormField>
        <FormField label="Email"><input className="field" type="email" {...form.register("email")} /></FormField>
        <FormField label="Source">
          <select className="field" {...form.register("source")}>{sources.map((source) => <option key={source}>{source}</option>)}</select>
        </FormField>
        <FormField label="Notes"><textarea className="field min-h-24 py-2" {...form.register("notes")} /></FormField>
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={create.isPending} submitLabel="Save lead" />
      </form>
    </FormDialog>
  );
}
