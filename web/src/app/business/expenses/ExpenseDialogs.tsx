"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { emptyToNull, type ExpenseCategory, type ExpenseRow } from "../../../lib/gym";

const methods = ["Cash", "Upi", "BankTransfer", "Card", "Other"] as const;
const expenseSchema = z.object({
  categoryId: z.string().min(1, "Choose a category"),
  description: z.string().min(1, "Enter a description").max(200),
  amount: z.string().regex(/^\d+(\.\d{1,4})?$/, "Enter an amount").refine((value) => Number(value) > 0, "Enter an amount"),
  expenseDate: z.string().min(1, "Choose a date"),
  paymentMethod: z.enum(methods),
  notes: z.string().max(2000),
});
const categorySchema = z.object({ name: z.string().min(1, "Enter a name").max(100), description: z.string().max(500) });

type ExpenseValues = z.infer<typeof expenseSchema>;
type CategoryValues = z.infer<typeof categorySchema>;

export function ExpenseDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: () => void }) {
  const categories = useQuery({ queryKey: ["expense-categories", "active"], queryFn: () => api<ExpenseCategory[]>("/expense-categories?activeOnly=true") });
  const form = useForm<ExpenseValues>({ resolver: zodResolver(expenseSchema), defaultValues: { categoryId: "", description: "", amount: "", expenseDate: "", paymentMethod: "Cash", notes: "" } });
  const create = useMutation({
    mutationFn: (values: ExpenseValues) => api<ExpenseRow>("/expenses", {
      method: "POST",
      body: JSON.stringify({ ...values, amount: Number(values.amount), notes: emptyToNull(values.notes) }),
    }),
    onSuccess: () => onCreated(),
  });

  return (
    <FormDialog title="Add expense" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => create.mutate(values))}>
        <FormField label="Category" error={form.formState.errors.categoryId?.message}>
          <select className="field" {...form.register("categoryId")}>
            <option value="">Select</option>
            {(categories.data ?? []).map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
          </select>
        </FormField>
        <FormField label="Description" error={form.formState.errors.description?.message}><input className="field" {...form.register("description")} /></FormField>
        <FormField label="Amount" error={form.formState.errors.amount?.message}><input className="field" step="0.01" type="number" {...form.register("amount")} /></FormField>
        <FormField label="Expense date" error={form.formState.errors.expenseDate?.message}><input className="field" type="date" {...form.register("expenseDate")} /></FormField>
        <FormField label="Payment method">
          <select className="field" {...form.register("paymentMethod")}>{methods.map((method) => <option key={method}>{method}</option>)}</select>
        </FormField>
        <FormField label="Notes"><textarea className="field min-h-24 py-2" {...form.register("notes")} /></FormField>
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={create.isPending} submitLabel="Save expense" />
      </form>
    </FormDialog>
  );
}

export function CategoryDialog({ onCancel, onCreated }: { onCancel: () => void; onCreated: () => void }) {
  const form = useForm<CategoryValues>({ resolver: zodResolver(categorySchema), defaultValues: { name: "", description: "" } });
  const create = useMutation({
    mutationFn: (values: CategoryValues) => api("/expense-categories", { method: "POST", body: JSON.stringify({ name: values.name, description: emptyToNull(values.description) }) }),
    onSuccess: () => onCreated(),
  });

  return (
    <FormDialog title="Add category" onCancel={onCancel}>
      <form className="mt-4 grid gap-3" onSubmit={form.handleSubmit((values) => create.mutate(values))}>
        <FormField label="Name" error={form.formState.errors.name?.message}><input className="field" {...form.register("name")} /></FormField>
        <FormField label="Description"><input className="field" {...form.register("description")} /></FormField>
        {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={create.isPending} submitLabel="Save category" />
      </form>
    </FormDialog>
  );
}
