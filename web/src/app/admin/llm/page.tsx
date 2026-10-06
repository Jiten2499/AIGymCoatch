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
import type { LlmConnection, LlmTest } from "../../../lib/types";

const schema = z.object({
  name: z.string().min(1).max(200),
  baseUrl: z.string().url(),
  apiKey: z.string().min(1).max(500),
  defaultModel: z.string().max(200),
  timeoutSeconds: z.number().int().min(1).max(60),
  isActive: z.boolean(),
  isDefault: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

export default function LlmPage() {
  const client = useQueryClient();
  const connections = useQuery({ queryKey: ["llm"], queryFn: () => api<LlmConnection[]>("/llm/connections") });
  const [pendingDelete, setPendingDelete] = useState<LlmConnection | null>(null);
  const [adding, setAdding] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { name: "", baseUrl: "https://api.groq.com/openai/v1", apiKey: "", defaultModel: "", timeoutSeconds: 20, isActive: true, isDefault: false } });

  const create = useMutation({
    mutationFn: (values: FormValues) => api<LlmConnection>("/llm/connections", { method: "POST", body: JSON.stringify({ ...values, provider: "OpenAiCompatible", defaultModel: values.defaultModel || null }) }),
    onSuccess: async () => {
      form.reset();
      setAdding(false);
      setNotice("Connection saved. The API key is not shown again.");
      await client.invalidateQueries({ queryKey: ["llm"] });
    },
  });
  const test = useMutation({
    mutationFn: (id: string) => api<LlmTest>(`/llm/connections/${id}/test`, { method: "POST" }),
    onSuccess: async (result) => {
      setNotice(result.succeeded ? `Connection successful${result.latencyMs !== null ? ` in ${result.latencyMs} ms` : ""}.` : result.message ?? "Connection failed.");
      await client.invalidateQueries({ queryKey: ["llm"] });
    },
  });
  const makeDefault = useMutation({
    mutationFn: (id: string) => api<LlmConnection>(`/llm/connections/${id}/default`, { method: "POST" }),
    onSuccess: async () => client.invalidateQueries({ queryKey: ["llm"] }),
  });
  const remove = useMutation({
    mutationFn: (id: string) => api(`/llm/connections/${id}`, { method: "DELETE" }),
    onSuccess: async () => {
      setPendingDelete(null);
      setSelectedId(null);
      await client.invalidateQueries({ queryKey: ["llm"] });
    },
  });

  const selected = (connections.data ?? []).find((row) => row.id === selectedId) ?? null;
  const actionError = create.error ?? test.error ?? makeDefault.error ?? remove.error;

  return (
    <PageState error={connections.error instanceof ApiError ? connections.error.message : null} loading={connections.isLoading} onRetry={() => void connections.refetch()}>
      <h1 className="text-3xl font-semibold">LLM connections</h1>
      <p className="mt-2 max-w-3xl text-muted">The platform executes chat through one OpenAI-compatible endpoint. Groq and OpenAI use this provider when the base URL matches their API. The key is sent once and is never returned.</p>
      <div className="mt-6 flex flex-wrap gap-3">
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => { form.reset(); setAdding(true); }} type="button">Add</button>
        <button className="min-h-11 rounded-full border border-line px-4 disabled:opacity-60" disabled={!selected || test.isPending} onClick={() => selected && test.mutate(selected.id)} type="button">{test.isPending ? "Testing..." : "Test connection"}</button>
        <button className="min-h-11 rounded-full border border-line px-4 disabled:opacity-60" disabled={!selected || selected.isDefault || makeDefault.isPending} onClick={() => selected && makeDefault.mutate(selected.id)} type="button">Make default</button>
        <button className="min-h-11 rounded-full border border-line px-4 disabled:opacity-60" disabled={!selected} onClick={() => selected && setPendingDelete(selected)} type="button">Delete</button>
      </div>
      <p className="mt-3 text-sm text-muted">{selected ? `Selected: ${selected.name}` : "Select a connection to test, make default, or delete it."}</p>
      {notice ? <p className="mt-4" role="status">{notice}</p> : null}
      {actionError instanceof ApiError ? <p className="mt-4" role="alert">{actionError.message}</p> : null}
      <div className="mt-6">
        <DataTable
          columns={[
            { header: "Name", value: (row) => row.name, cell: (row) => <Link href={`/admin/llm/${row.id}`}>{row.name}</Link> },
            { header: "Endpoint", cell: (row) => row.baseUrl },
            { header: "Key", cell: (row) => (row.apiKeyConfigured ? "Configured" : "Missing") },
            { header: "Status", cell: (row) => `${row.healthStatus}${row.isDefault ? " · Default" : ""}` },
            { header: "Models", cell: (row) => String(row.modelCount) },
          ]}
          empty="No LLM connections are configured."
          rowKey={(row) => row.id}
          rows={connections.data ?? []}
          selection={{
            isSelected: (row) => row.id === selectedId,
            label: (row) => `Select ${row.name}`,
            onToggle: (row) => setSelectedId(row.id === selectedId ? null : row.id),
          }}
        />
      </div>
      {adding ? (
        <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation">
          <form aria-labelledby="add-connection-title" aria-modal="true" className="grid w-full max-w-xl gap-4 rounded-2xl border border-line bg-surface p-6" onSubmit={form.handleSubmit((values) => create.mutate(values))} role="dialog">
            <h2 className="text-xl font-semibold" id="add-connection-title">Add connection</h2>
            <Field label="Name" error={form.formState.errors.name?.message}><input className="field" {...form.register("name")} /></Field>
            <Field label="Base URL" error={form.formState.errors.baseUrl?.message}><input className="field" {...form.register("baseUrl")} /></Field>
            <Field label="API key" error={form.formState.errors.apiKey?.message}><input autoComplete="off" className="field" type="password" {...form.register("apiKey")} /></Field>
            <Field label="Default model" error={form.formState.errors.defaultModel?.message}><input className="field" {...form.register("defaultModel")} /></Field>
            <Field label="Timeout seconds" error={form.formState.errors.timeoutSeconds?.message}><input className="field" type="number" {...form.register("timeoutSeconds", { valueAsNumber: true })} /></Field>
            <label className="flex min-h-11 items-center gap-2"><input type="checkbox" {...form.register("isActive")} /> Active</label>
            <label className="flex min-h-11 items-center gap-2"><input type="checkbox" {...form.register("isDefault")} /> Default connection</label>
            {create.error instanceof ApiError ? <p role="alert">{create.error.message}</p> : null}
            <div className="flex justify-end gap-3">
              <button className="min-h-11 rounded-full px-4" onClick={() => setAdding(false)} type="button">Cancel</button>
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={create.isPending} type="submit">{create.isPending ? "Saving..." : "Save connection"}</button>
            </div>
          </form>
        </div>
      ) : null}
      {pendingDelete ? (
        <ConfirmDialog
          body={pendingDelete.isDefault ? "The API will reject deletion of the default connection until another default is chosen." : `Delete ${pendingDelete.name}? The stored key is removed and is not shown.`}
          confirmLabel="Delete connection"
          onCancel={() => setPendingDelete(null)}
          onConfirm={() => remove.mutate(pendingDelete.id)}
          pending={remove.isPending}
          title="Delete LLM connection"
        />
      ) : null}
    </PageState>
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
