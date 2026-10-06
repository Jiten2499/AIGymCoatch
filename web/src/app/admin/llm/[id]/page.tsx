"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useState } from "react";
import { PageState } from "../../../../components/AdminShell";
import { DataTable } from "../../../../components/DataTable";
import { api, ApiError } from "../../../../lib/api";
import type { LlmConnection, LlmModel, LlmTest } from "../../../../lib/types";

export default function LlmDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const client = useQueryClient();
  const connections = useQuery({ queryKey: ["llm"], queryFn: () => api<LlmConnection[]>("/llm/connections") });
  const models = useQuery({ queryKey: ["llm-models", id], queryFn: () => api<LlmModel[]>(`/llm/connections/${id}/models`) });
  const connection = connections.data?.find((item) => item.id === id) ?? null;
  const [identifier, setIdentifier] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [notice, setNotice] = useState<string | null>(null);
  const addModel = useMutation({
    mutationFn: () => api<LlmModel>(`/llm/connections/${id}/models`, { method: "POST", body: JSON.stringify({ modelIdentifier: identifier, displayName: displayName || identifier, isActive: true, isDefault: false }) }),
    onSuccess: async () => {
      setIdentifier("");
      setDisplayName("");
      await client.invalidateQueries({ queryKey: ["llm-models", id] });
      await client.invalidateQueries({ queryKey: ["llm"] });
    },
  });
  const refresh = useMutation({
    mutationFn: () => api<LlmTest>(`/llm/connections/${id}/models/refresh`, { method: "POST" }),
    onSuccess: (result) => setNotice(result.succeeded ? `Provider returned ${result.models.length} model ids. They are not saved until you add one.` : result.message ?? "Refresh failed."),
  });
  const toggleDefault = useMutation({
    mutationFn: (model: LlmModel) => api(`/llm/models/${model.id}`, { method: "PUT", body: JSON.stringify({ displayName: model.displayName, isActive: model.isActive, isDefault: !model.isDefault }) }),
    onSuccess: async () => client.invalidateQueries({ queryKey: ["llm-models", id] }),
  });

  const error = connections.error ?? models.error ?? addModel.error ?? refresh.error ?? toggleDefault.error;

  return (
    <PageState error={connections.isError || models.isError ? (error instanceof ApiError ? error.message : "The connection could not be loaded.") : null} loading={connections.isLoading || models.isLoading} onRetry={() => { void connections.refetch(); void models.refetch(); }}>
      <h1 className="text-3xl font-semibold">{connection?.name ?? "Connection"}</h1>
      <p className="mt-2 text-muted">{connection ? `${connection.baseUrl} · API key ${connection.apiKeyConfigured ? "configured" : "missing"} · ${connection.healthStatus}` : "That connection is not in the current list."}</p>
      {notice ? <p className="mt-4" role="status">{notice}</p> : null}
      {error instanceof ApiError && !connections.isError && !models.isError ? <p className="mt-4" role="alert">{error.message}</p> : null}
      {refresh.data?.models.length ? (
        <ul className="mt-4 list-disc pl-5 text-sm text-muted">
          {refresh.data.models.slice(0, 20).map((model) => <li key={model}>{model}</li>)}
        </ul>
      ) : null}
      <div className="mt-6 flex flex-wrap gap-3">
        <button className="min-h-11 rounded-full border border-line px-4" disabled={refresh.isPending} onClick={() => refresh.mutate()} type="button">{refresh.isPending ? "Refreshing..." : "Refresh models"}</button>
      </div>
      {connection ? <ConnectionForm key={connection.id} connection={connection} onSaved={(message) => { setNotice(message); void client.invalidateQueries({ queryKey: ["llm"] }); }} /> : null}
      <form className="mt-8 grid gap-3 md:grid-cols-[1fr_1fr_auto]" onSubmit={(event) => { event.preventDefault(); addModel.mutate(); }}>
        <input aria-label="Model identifier" className="field" onChange={(event) => setIdentifier(event.target.value)} placeholder="Model identifier" value={identifier} />
        <input aria-label="Display name" className="field" onChange={(event) => setDisplayName(event.target.value)} placeholder="Display name" value={displayName} />
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={identifier.trim().length === 0 || addModel.isPending} type="submit">Add model</button>
      </form>
      <p className="mt-4 text-sm text-muted">Saved models are used as labels. The connection default model is what chat requests send. The only executed capability is chat.</p>
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Model", cell: (row) => row.modelIdentifier },
            { header: "Name", cell: (row) => row.displayName },
            { header: "Capability", cell: (row) => row.capabilities.join(", ") },
            { header: "Default", cell: (row) => (row.isDefault ? "Default" : "No") },
            { header: "Action", cell: (row) => <button className="min-h-11" disabled={toggleDefault.isPending} onClick={() => toggleDefault.mutate(row)} type="button">{row.isDefault ? "Clear default" : "Make default"}</button> },
          ]}
          empty="No models are saved for this connection."
          rowKey={(row) => row.id}
          rows={models.data ?? []}
        />
      </div>
    </PageState>
  );
}

function ConnectionForm({ connection, onSaved }: { connection: LlmConnection; onSaved: (message: string) => void }) {
  const [name, setName] = useState(connection.name);
  const [baseUrl, setBaseUrl] = useState(connection.baseUrl);
  const [defaultModel, setDefaultModel] = useState(connection.defaultModel ?? "");
  const [timeoutSeconds, setTimeoutSeconds] = useState(String(connection.timeoutSeconds));
  const [isActive, setIsActive] = useState(connection.isActive);
  const [replaceKey, setReplaceKey] = useState(false);
  const [apiKey, setApiKey] = useState("");
  const save = useMutation({
    mutationFn: () => api<LlmConnection>(`/llm/connections/${connection.id}`, {
      method: "PUT",
      body: JSON.stringify({
        name: name.trim(),
        baseUrl: baseUrl.trim(),
        apiKey: replaceKey ? apiKey : null,
        defaultModel: defaultModel.trim() || null,
        isActive,
        isDefault: connection.isDefault,
        timeoutSeconds: Number(timeoutSeconds),
      }),
    }),
    onSuccess: () => {
      setApiKey("");
      setReplaceKey(false);
      onSaved("Saved. The API key remains hidden.");
    },
  });

  return (
    <form className="mt-8 grid max-w-xl gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
      <h2 className="text-xl font-semibold">Connection settings</h2>
      {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
      <label className="grid gap-2">Name<input aria-label="Connection name" className="field" onChange={(event) => setName(event.target.value)} value={name} /></label>
      <label className="grid gap-2">Base URL<input aria-label="Base URL" className="field" onChange={(event) => setBaseUrl(event.target.value)} value={baseUrl} /></label>
      <label className="grid gap-2">Default model<input aria-label="Default model" className="field" onChange={(event) => setDefaultModel(event.target.value)} value={defaultModel} /></label>
      <label className="grid gap-2">Timeout seconds<input aria-label="Timeout seconds" className="field" max={60} min={1} onChange={(event) => setTimeoutSeconds(event.target.value)} type="number" value={timeoutSeconds} /></label>
      <label className="flex min-h-11 items-center gap-2"><input checked={isActive} disabled={connection.isDefault} onChange={(event) => setIsActive(event.target.checked)} type="checkbox" /> Active{connection.isDefault ? " (the default stays active)" : ""}</label>
      <p>API key: {connection.apiKeyConfigured ? "Configured" : "Missing"}</p>
      <label className="flex min-h-11 items-center gap-2"><input checked={replaceKey} onChange={(event) => setReplaceKey(event.target.checked)} type="checkbox" /> Replace API key</label>
      {replaceKey ? <input aria-label="Replacement API key" autoComplete="off" className="field" onChange={(event) => setApiKey(event.target.value)} type="password" value={apiKey} /> : null}
      <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={save.isPending || name.trim().length === 0 || (replaceKey && apiKey.length === 0)} type="submit">{save.isPending ? "Saving..." : "Save connection"}</button>
    </form>
  );
}
