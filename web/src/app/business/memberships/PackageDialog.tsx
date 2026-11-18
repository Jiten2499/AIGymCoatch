"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { emptyToNull, type MembershipPackage } from "../../../lib/gym";

const schema = z.object({
  name: z.string().min(1, "Enter a package name").max(200),
  description: z.string().max(500),
  membershipType: z.enum(["Monthly", "Quarterly", "HalfYearly", "Yearly", "Custom"]),
  category: z.enum(["Standard", "Student", "Women", "Corporate", "Couple"]),
  durationUnit: z.enum(["Day", "Month"]),
  durationCount: z.string().regex(/^[1-9]\d*$/, "Enter a duration"),
  price: z.string().regex(/^\d+(\.\d{1,2})?$/, "Enter a price"),
  includedPtSessions: z.string().regex(/^\d+$/, "Enter included PT sessions"),
});

type Values = z.infer<typeof schema>;

export function PackageDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: () => void }) {
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", description: "", membershipType: "Monthly", category: "Standard", durationUnit: "Month", durationCount: "1", price: "0", includedPtSessions: "0" },
  });
  const profile = useQuery({ queryKey: ["business-profile", "package-currency"], queryFn: () => api<{ currency: string }>("/business/profile") });
  const currency = profile.data?.currency || "USD";
  const create = useMutation({
    mutationFn: (values: Values) => api<MembershipPackage>("/membership-plans", {
      method: "POST",
      body: JSON.stringify({
        name: values.name,
        description: emptyToNull(values.description),
        durationDays: values.durationUnit === "Month" ? Number(values.durationCount) * 30 : Number(values.durationCount),
        membershipType: values.membershipType,
        maxVisits: null,
        price: Number(values.price),
        currency,
        category: values.category,
        durationUnit: values.durationUnit,
        durationCount: Number(values.durationCount),
        includedPtSessions: Number(values.includedPtSessions),
      }),
    }),
    onSuccess: () => onCreated(),
  });

  return (
    <FormDialog title="Add package" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => create.mutate(values))}>
        <FormField label="Name" error={form.formState.errors.name?.message}><input className="field" {...form.register("name")} /></FormField>
        <FormField label="Description"><input className="field" {...form.register("description")} /></FormField>
        <FormField label="Type">
          <select className="field" {...form.register("membershipType")}>
            <option>Monthly</option><option>Quarterly</option><option>HalfYearly</option><option>Yearly</option><option>Custom</option>
          </select>
        </FormField>
        <FormField label="Category">
          <select className="field" {...form.register("category")}>
            <option>Standard</option><option>Student</option><option>Women</option><option>Corporate</option><option>Couple</option>
          </select>
        </FormField>
        <FormField label="Duration" error={form.formState.errors.durationCount?.message}>
          <span className="flex gap-2">
            <input className="field" type="number" {...form.register("durationCount")} />
            <select className="field" {...form.register("durationUnit")}><option>Month</option><option>Day</option></select>
          </span>
        </FormField>
        <FormField label="Price" error={form.formState.errors.price?.message}>
          <span className="flex gap-2">
            <input className="field" step="0.01" type="number" {...form.register("price")} />
            <input aria-label="Currency" className="field w-28" readOnly value={currency} />
          </span>
        </FormField>
        <FormField label="Included PT sessions" error={form.formState.errors.includedPtSessions?.message}>
          <input className="field" min={0} type="number" {...form.register("includedPtSessions")} />
        </FormField>
        <p className="text-sm text-muted">Use 0 when this membership does not include personal-training sessions. Included sessions are not a separate package sale.</p>
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={create.isPending} submitLabel="Save package" />
      </form>
    </FormDialog>
  );
}
