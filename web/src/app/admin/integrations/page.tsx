"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { PageState } from "../../../components/AdminShell";
import { api, ApiError } from "../../../lib/api";

type StorageConnection = {
  provider: string;
  status: string;
  accountEmail: string | null;
  connectedAt: string | null;
  lastValidatedAt: string | null;
  lastError: string | null;
  canConnect: boolean;
  storeProgressPhotos: boolean;
  progressFolderId: string | null;
  progressFolderName: string | null;
  clientId: string | null;
  hasClientSecret: boolean;
  redirectUri: string | null;
};

type DriveFolder = { id: string; name: string; current: boolean };

export default function GoogleDriveAdminPage() {
  const client = useQueryClient();
  const [notice, setNotice] = useState<string | null>(null);
  const [foldersOpen, setFoldersOpen] = useState(false);
  const [clientId, setClientId] = useState("");
  const [clientSecret, setClientSecret] = useState("");
  const [redirectUri, setRedirectUri] = useState("");
  const [formReady, setFormReady] = useState(false);
  const connection = useQuery({
    queryKey: ["platform-google-drive"],
    queryFn: () => api<StorageConnection>("/integrations/google-drive"),
  });
  const folders = useQuery({
    queryKey: ["platform-google-drive-folders"],
    enabled: foldersOpen && connection.data?.status === "Connected",
    queryFn: () => api<DriveFolder[]>("/integrations/google-drive/folders"),
  });

  useEffect(() => {
    if (connection.data && !formReady) {
      setClientId(connection.data.clientId ?? "");
      setRedirectUri(connection.data.redirectUri ?? "");
      setFormReady(true);
    }
  }, [connection.data, formReady]);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const status = params.get("status");
    if (status === "connected") setNotice("Google Drive is connected for every gym.");
    if (status === "failed") setNotice("Google Drive could not be connected. Approve access and try again.");
    if (status) {
      void client.invalidateQueries({ queryKey: ["platform-google-drive"] });
      window.history.replaceState({}, "", window.location.pathname);
    }
  }, [client]);

  const refresh = async () => { await client.invalidateQueries({ queryKey: ["platform-google-drive"] }); };
  const save = useMutation({
    mutationFn: () => api<StorageConnection>("/integrations/google-drive/app", {
      method: "PUT",
      body: JSON.stringify({ clientId, clientSecret, redirectUri }),
    }),
    onSuccess: async (saved) => {
      setClientId(saved.clientId ?? "");
      setRedirectUri(saved.redirectUri ?? "");
      setClientSecret("");
      setNotice("Google OAuth app saved.");
      await refresh();
    },
  });
  const connect = useMutation({
    mutationFn: () => api<{ authorizationUrl: string }>("/integrations/google-drive/connect", {
      method: "POST",
      body: JSON.stringify({ clientKind: "Web", returnPath: null }),
    }),
    onSuccess: (data) => { window.location.href = data.authorizationUrl; },
  });
  const test = useMutation({
    mutationFn: () => api<StorageConnection>("/integrations/google-drive/test", { method: "POST" }),
    onSuccess: async () => { setNotice("Connection succeeded."); await refresh(); },
  });
  const disconnect = useMutation({
    mutationFn: () => api<StorageConnection>("/integrations/google-drive/disconnect", { method: "POST" }),
    onSuccess: async () => { setNotice("Google Drive was disconnected. New progress photos stay in LifeFit until you connect again."); setFoldersOpen(false); await refresh(); },
  });
  const toggle = useMutation({
    mutationFn: (value: boolean) => api<StorageConnection>("/integrations/google-drive/progress-photos", { method: "PUT", body: JSON.stringify({ enabled: value }) }),
    onSuccess: async (data) => { setNotice(data.storeProgressPhotos ? "Progress photos will be stored in Google Drive." : "Automatic Drive storage is off."); await refresh(); },
  });
  const chooseFolder = useMutation({
    mutationFn: (folderId: string) => api<StorageConnection>("/integrations/google-drive/folder", { method: "POST", body: JSON.stringify({ folderId }) }),
    onSuccess: async (data) => { setNotice(`Destination folder is ${data.progressFolderName ?? "the selected folder"}.`); setFoldersOpen(false); await refresh(); },
  });

  const data = connection.data;
  const connected = data?.status === "Connected";
  const actionError = save.error ?? connect.error ?? test.error ?? disconnect.error ?? toggle.error ?? chooseFolder.error ?? folders.error;
  const destination = data?.progressFolderName ?? (connected ? "LifeFit / Members" : "Not selected");

  return (
    <PageState error={connection.error instanceof ApiError ? connection.error.message : null} loading={connection.isLoading} onRetry={() => void connection.refetch()}>
      <p className="text-sm text-muted">System Settings &gt; Google Drive</p>
      <h1 className="mt-2 text-3xl font-semibold">Google Drive</h1>
      <p className="mt-2 max-w-3xl text-muted">One Google Drive account for the whole platform. Gyms store files here when their plan includes Google Drive storage.</p>

      <section className="mt-6 max-w-2xl rounded-2xl border border-line bg-surface px-6 py-5">
        <h2 className="text-xl font-semibold">OAuth app</h2>
        <p className="mt-2 text-sm text-muted">Create a Google Cloud OAuth client and register this exact redirect URI. It must be the API address, <span className="text-foreground">https://api.growventur.com/api/v1/integrations/google-drive/callback</span>. The website address does not complete the connection. The client secret is stored protected and is not shown again.</p>
        <label className="mt-4 block text-sm text-muted">
          Client ID
          <input className="field mt-2 w-full" value={clientId} onChange={(event) => setClientId(event.target.value)} autoComplete="off" />
        </label>
        <label className="mt-4 block text-sm text-muted">
          Client secret
          <input className="field mt-2 w-full" type="password" value={clientSecret} onChange={(event) => setClientSecret(event.target.value)} placeholder={data?.hasClientSecret ? "Saved. Leave blank to keep it." : "Paste the client secret"} autoComplete="new-password" />
        </label>
        <label className="mt-4 block text-sm text-muted">
          Redirect URI
          <input className="field mt-2 w-full" value={redirectUri} onChange={(event) => setRedirectUri(event.target.value)} autoComplete="off" />
        </label>
        <button className="mt-4 min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-40" disabled={save.isPending || clientId.trim().length === 0 || redirectUri.trim().length === 0} onClick={() => save.mutate()} type="button">
          {save.isPending ? "Saving..." : "Save OAuth app"}
        </button>
      </section>

      <section className="mt-6 max-w-2xl rounded-2xl border border-line bg-surface px-6 py-5">
        <h2 className="text-xl font-semibold">Connection</h2>
        <dl className="mt-4 grid gap-2 text-sm">
          <div className="flex flex-wrap gap-2"><dt className="text-muted">Status:</dt><dd>{statusText(data?.status)}</dd></div>
          <div className="flex flex-wrap gap-2"><dt className="text-muted">Account:</dt><dd>{data?.accountEmail ?? "Not connected"}</dd></div>
          <div className="flex flex-wrap gap-2"><dt className="text-muted">Destination folder:</dt><dd>{destination}</dd></div>
          {data?.lastValidatedAt ? <div className="flex flex-wrap gap-2"><dt className="text-muted">Last checked:</dt><dd>{new Date(data.lastValidatedAt).toLocaleString()}</dd></div> : null}
          {data?.lastError ? <div className="flex flex-wrap gap-2"><dt className="text-muted">Last error:</dt><dd>{data.lastError}</dd></div> : null}
        </dl>
        {connected ? (
          <label className="mt-5 flex items-center gap-3 text-sm">
            <input checked={data?.storeProgressPhotos === true} disabled={toggle.isPending} onChange={(event) => toggle.mutate(event.target.checked)} type="checkbox" />
            Store new progress photos in Google Drive automatically
          </label>
        ) : null}
        <div className="mt-5 flex flex-wrap gap-2">
          {!connected ? (
            <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:cursor-not-allowed disabled:opacity-40" disabled={!data?.canConnect || connect.isPending} onClick={() => connect.mutate()} type="button">
              {connect.isPending ? "Connecting..." : data?.status === "Revoked" || data?.status === "Failed" ? "Reconnect Google Drive" : "Connect Google Drive"}
            </button>
          ) : (
            <>
              <button className="min-h-11 rounded-full border border-line px-4" disabled={test.isPending} onClick={() => test.mutate()} type="button">{test.isPending ? "Testing..." : "Test Connection"}</button>
              <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setFoldersOpen((open) => !open)} type="button">Change Folder</button>
              <button className="min-h-11 rounded-full border border-line px-4" disabled={disconnect.isPending} onClick={() => disconnect.mutate()} type="button">Disconnect</button>
            </>
          )}
        </div>
        {data && !data.canConnect ? <p className="mt-3 text-sm text-muted">Save the OAuth client ID, secret, and redirect URI before connecting.</p> : null}
        {data?.status === "Revoked" ? <p className="mt-3 text-sm">Authorization expired or was revoked. Reconnect before uploading progress photos.</p> : null}
        {actionError instanceof ApiError ? <p className="mt-3 text-sm" role="alert">{actionError.message}</p> : null}
        {notice ? <p className="mt-3 text-sm" role="status">{notice}</p> : null}
        {foldersOpen ? (
          <div className="mt-4 grid gap-2">
            {folders.isLoading ? <p className="text-sm text-muted">Loading folders...</p> : null}
            {folders.data?.map((folder) => (
              <button className="min-h-11 rounded-xl border border-line px-3 text-left" disabled={chooseFolder.isPending} key={folder.id} onClick={() => chooseFolder.mutate(folder.id)} type="button">
                {folder.name}{folder.current ? " · current" : ""}
              </button>
            ))}
            {folders.data?.length === 0 ? <p className="text-sm text-muted">No folders were found under LifeFit.</p> : null}
          </div>
        ) : null}
      </section>
    </PageState>
  );
}

function statusText(status: string | undefined) {
  if (status === "Connected") return "Connected";
  if (status === "Revoked") return "Authorization revoked";
  if (status === "Failed") return "Connection failed";
  if (status === "Connecting") return "Connecting";
  return "Not connected";
}
