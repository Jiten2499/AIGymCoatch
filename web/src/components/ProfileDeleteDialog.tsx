"use client";

import Link from "next/link";

export const activeSeatDeletionMessage =
  "This user currently has an assigned business seat. Revoke the seat before deleting this profile.";

export function ProfileDeleteDialog({
  title,
  name,
  error,
  pending,
  onCancel,
  onConfirm,
}: {
  title: string;
  name: string;
  error?: string | null;
  pending?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const blocked = Boolean(error?.includes(activeSeatDeletionMessage));
  return (
    <div className="fixed inset-0 z-30 flex items-center justify-center bg-black/60 p-4" role="presentation">
      <div aria-modal="true" className="w-full max-w-lg rounded-2xl border border-line bg-surface p-6" role="dialog">
        <h2 className="text-xl font-semibold">{title}</h2>
        <p className="mt-3 text-muted">
          Delete {name}? This removes the profile from active business views. The record is retained as soft-deleted data and is not erased.
        </p>
        {error && !blocked ? <p className="mt-3" role="alert">{error}</p> : null}
        {blocked ? (
          <div className="mt-3 grid gap-3" role="alert">
            <p>{activeSeatDeletionMessage}</p>
            <Link className="inline-flex min-h-11 items-center justify-center rounded-full border border-line px-4" href="/business/plans">Open plans and seats</Link>
          </div>
        ) : null}
        <div className="mt-6 flex justify-end gap-3">
          <button className="min-h-11 rounded-full px-4" onClick={onCancel} type="button">Cancel</button>
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={pending || blocked} onClick={onConfirm} type="button">
            {pending ? "Working..." : "Delete"}
          </button>
        </div>
      </div>
    </div>
  );
}
