"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { api, ApiError } from "../../../lib/api";
import { getSession, subscribeSession } from "../../../lib/session";

type StorageConnection = {
  provider: string;
  status: string;
  accountEmail: string | null;
  rootFolderId: string | null;
  connectedAt: string | null;
  lastValidatedAt: string | null;
  lastError: string | null;
  canConnect: boolean;
  storeProgressPhotos: boolean;
  progressFolderId: string | null;
  progressFolderName: string | null;
};

type DriveFolder = { id: string; name: string; current: boolean };

export function GoogleDriveSection() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const [notice, setNotice] = useState<string | null>(null);
  const [foldersOpen, setFoldersOpen] = useState(false);
  const features = useQuery({
    queryKey: ["features", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<{ featureCode: string; enabled: boolean }[]>("/features"),
  });
  const enabled = features.data?.some((feature) => feature.featureCode === "GOOGLE_DRIVE_STORAGE" && feature.enabled) === true;
  const connection = useQuery({
    queryKey: ["google-drive-connection", tenantId],
    enabled: tenantId !== null && enabled,
    queryFn: () => api<StorageConnection>("/integrations/google-drive"),
  });
  const folders = useQuery({
    queryKey: ["google-drive-folders", tenantId],
    enabled: foldersOpen && connection.data?.status === "Connected",
    queryFn: () => api<DriveFolder[]>("/integrations/google-drive/folders"),
  });

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const status = params.get("status");
    if (status === "connected") {
      setNotice("Google Drive is connected.");
    }
    if (status === "failed") {
      setNotice("Google Drive could not be connected. Approve access and try again.");
    }
    if (status) {
      void client.invalidateQueries({ queryKey: ["google-drive-connection", tenantId] });
      window.history.replaceState({}, "", window.location.pathname);
    }
  }, [client, tenantId]);

  const refresh = async () => { await client.invalidateQueries({ queryKey: ["google-drive-connection", tenantId] }); };
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

  if (features.isLoading) {
    return <PageState error={null} loading onRetry={() => void features.refetch()}><span /></PageState>;
  }
  if (!enabled) {
    return <p className="mt-4 text-muted">Google Drive storage is not enabled for this workspace.</p>;
  }

  const data = connection.data;
  const connected = data?.status === "Connected";
  const actionError = connect.error ?? test.error ?? disconnect.error ?? toggle.error ?? chooseFolder.error ?? folders.error;
  const destination = data?.progressFolderName ?? (connected ? "LifeFit / Members" : "Not selected");

  return (
    <PageState error={connection.error instanceof ApiError ? connection.error.message : null} loading={connection.isLoading} onRetry={() => void connection.refetch()}>
      <section className="max-w-2xl rounded-2xl border border-line bg-surface px-6 py-5 text-foreground">
        <h2 className="text-xl font-semibold text-foreground">Google Drive</h2>
        <p className="mt-2 text-muted">Private storage for member progress photos in this business.</p>
        <dl className="mt-4 grid gap-2 text-sm text-foreground">
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
        {data && !data.canConnect ? <p className="mt-3 text-sm text-muted">Google OAuth is not configured on the API server yet.</p> : null}
        {data?.status === "Revoked" ? <p className="mt-3 text-sm">Authorization expired or was revoked. Reconnect before uploading progress photos.</p> : null}
        {actionError instanceof ApiError ? <p className="mt-3 text-sm" role="alert">{actionError.message}</p> : null}
        {notice ? <p className="mt-3 text-sm">{notice}</p> : null}
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
