"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { ApiError, removeProfilePhoto, uploadProfilePhoto } from "../lib/api";
import { getSession, setSession, subscribeSession } from "../lib/session";
import { UserAvatar } from "./UserAvatar";

const allowed = new Set(["image/jpeg", "image/png", "image/webp"]);

export function ProfilePhotoCard() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const user = session?.user;
  const [preview, setPreview] = useState<string | null>(null);
  const [progress, setProgress] = useState<number | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const busy = progress !== null;

  async function choose(file: File | undefined) {
    if (!file || !user) {
      return;
    }
    setMessage(null);
    if (!allowed.has(file.type) || file.size === 0 || file.size > 2_097_152) {
      setError("Choose a JPEG, PNG, or WebP image under 2 MB.");
      return;
    }

    setError(null);
    const local = URL.createObjectURL(file);
    setPreview(local);
    setProgress(0);
    try {
      const saved = await uploadProfilePhoto(file, (ratio) => setProgress(ratio));
      const current = getSession();
      if (current) {
        setSession({ ...current, user: { ...current.user, hasPhoto: saved.hasPhoto } });
      }
      await client.invalidateQueries({ queryKey: ["user-photo", user.id] });
      setMessage("Profile picture saved.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "The profile picture could not be saved.");
    } finally {
      setProgress(null);
      setPreview(null);
      URL.revokeObjectURL(local);
    }
  }

  async function remove() {
    if (!user) {
      return;
    }
    setError(null);
    setMessage(null);
    setProgress(0);
    try {
      const saved = await removeProfilePhoto();
      const current = getSession();
      if (current) {
        setSession({ ...current, user: { ...current.user, hasPhoto: saved.hasPhoto } });
      }
      await client.invalidateQueries({ queryKey: ["user-photo", user.id] });
      setMessage("Profile picture removed.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "The profile picture could not be removed.");
    } finally {
      setProgress(null);
    }
  }

  return (
    <section className="max-w-xl rounded-2xl border border-line bg-surface px-6 py-5 text-foreground">
      <h2 className="text-xl font-semibold">Profile picture</h2>
      <p className="mt-2 text-muted">This picture is private to your account and shown wherever your name appears.</p>
      <div className="mt-5 flex items-center gap-4">
        {preview ? <img alt="" className="h-24 w-24 rounded-full object-cover" src={preview} /> : <UserAvatar firstName={user?.firstName} hasPhoto={user?.hasPhoto !== false} lastName={user?.lastName} size={96} userId={user?.id} />}
        <div>
          <p className="font-semibold">{user ? `${user.firstName} ${user.lastName}`.trim() || user.email : ""}</p>
          <p className="text-sm text-muted">{user?.email}</p>
        </div>
      </div>
      <div className="mt-5 flex flex-wrap gap-2">
        <label className="min-h-11 cursor-pointer rounded-full bg-accent px-4 py-3 font-semibold text-background">
          {busy ? `Uploading ${Math.round((progress ?? 0) * 100)}%` : "Choose photo"}
          <input accept="image/jpeg,image/png,image/webp" className="sr-only" disabled={busy} onChange={(event) => { void choose(event.target.files?.[0]); event.target.value = ""; }} type="file" />
        </label>
        <label className="min-h-11 cursor-pointer rounded-full border border-line px-4 py-3">
          Take photo
          <input accept="image/jpeg,image/png,image/webp" capture="user" className="sr-only" disabled={busy} onChange={(event) => { void choose(event.target.files?.[0]); event.target.value = ""; }} type="file" />
        </label>
        <button className="min-h-11 rounded-full border border-line px-4" disabled={busy || user?.hasPhoto === false} onClick={() => void remove()} type="button">Remove</button>
      </div>
      {message ? <p className="mt-3 text-sm" role="status">{message}</p> : null}
      {error ? <p className="mt-3 text-sm" role="alert">{error}</p> : null}
    </section>
  );
}
