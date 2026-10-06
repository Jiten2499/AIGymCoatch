"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { PageState } from "../../../components/AdminShell";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { Plan } from "../../../lib/types";

const schema = z.object({
  code: z.string().min(1).max(64).regex(/^[A-Za-z0-9_]+$/, "Use letters, numbers, or underscores"),
  name: z.string().min(1).max(200),
  description: z.string().max(1000),
  appliesTo: z.enum(["Personal", "Business", "Any"]),
  sortOrder: z.number().int(),
  baseCapacity: z.number().int().min(0),
});

type FormValues = z.infer<typeof schema>;

export default function PlansPage() {
  const client = useQueryClient();
  const plans = useQuery({ queryKey: ["plans"], queryFn: () => api<Plan[]>("/platform/plans") });
  const [retire, setRetire] = useState<Plan | null>(null);
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { code: "", name: "", description: "", appliesTo: "Any", sortOrder: 0, baseCapacity: 1 } });
  const create = useMutation({
    mutationFn: (values: FormValues) => api<Plan>("/platform/plans", { method: "POST", body: JSON.stringify({ ...values, description: values.description || null }) }),
    onSuccess: async () => {
      form.reset();
      await client.invalidateQueries({ queryKey: ["plans"] });
    },
  });
  const change = useMutation({
    mutationFn: ({ id, action }: { id: string; action: "activate" | "deactivate" | "retire" }) => api(`/platform/plans/${id}/${action}`, { method: "POST" }),
    onSuccess: async () => {
      setRetire(null);
      await client.invalidateQueries({ queryKey: ["plans"] });
    },
  });
  const error = plans.error ?? create.error ?? change.error;

  return (
    <PageState error={plans.error instanceof ApiError ? plans.error.message : null} loading={plans.isLoading} onRetry={() => void plans.refetch()}>
      <h1 className="text-3xl font-semibold">Plans</h1>
      <p className="mt-2 text-muted">Lifecycle changes follow the API. A retired plan cannot be activated again.</p>
      {error instanceof ApiError && !plans.isError ? <p className="mt-4" role="alert">{error.message}</p> : null}
      <form className="mt-6 grid gap-3 md:grid-cols-3" onSubmit={form.handleSubmit((values) => create.mutate(values))}>
        <input aria-label="Plan code" className="field" placeholder="Code" {...form.register("code")} />
        <input aria-label="Plan name" className="field" placeholder="Name" {...form.register("name")} />
        <select aria-label="Applies to" className="field" {...form.register("appliesTo")}>
          <option>Any</option>
          <option>Personal</option>
          <option>Business</option>
        </select>
        <input aria-label="Description" className="field" placeholder="Description" {...form.register("description")} />
        <input aria-label="Sort order" className="field" type="number" {...form.register("sortOrder", { valueAsNumber: true })} />
        <input aria-label="Base capacity" className="field" type="number" {...form.register("baseCapacity", { valueAsNumber: true })} />
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={create.isPending} type="submit">{create.isPending ? "Saving..." : "Create plan"}</button>
      </form>
      <div className="mt-8">
        <DataTable
          columns={[
            { header: "Code", value: (row) => row.code, cell: (row) => <Link href={`/admin/plans/${row.id}`}>{row.code}</Link> },
            { header: "Name", cell: (row) => row.name },
            { header: "Status", cell: (row) => row.status },
            { header: "Applies to", cell: (row) => row.appliesTo },
            { header: "Base seats", cell: (row) => String(row.baseCapacity) },
            { header: "Actions", cell: (row) => (
              <div className="flex flex-wrap gap-2">
                <button className="min-h-11" disabled={change.isPending} onClick={() => change.mutate({ id: row.id, action: "activate" })} type="button">Activate</button>
                <button className="min-h-11" disabled={change.isPending} onClick={() => change.mutate({ id: row.id, action: "deactivate" })} type="button">Deactivate</button>
                <button className="min-h-11" onClick={() => setRetire(row)} type="button">Retire</button>
              </div>
            ) },
          ]}
          empty="No plans are available."
          rowKey={(row) => row.id}
          rows={plans.data ?? []}
        />
      </div>
      {retire ? <ConfirmDialog body={`${retire.code} will be retired. The API rejects activating it afterward.`} confirmLabel="Retire plan" onCancel={() => setRetire(null)} onConfirm={() => change.mutate({ id: retire.id, action: "retire" })} pending={change.isPending} title="Retire plan" /> : null}
    </PageState>
  );
}
