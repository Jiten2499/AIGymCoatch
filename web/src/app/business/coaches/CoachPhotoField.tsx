"use client";

import { useEffect, useMemo } from "react";
import { apiRoot, ApiError } from "../../../lib/api";
import { getSession } from "../../../lib/session";
import { nameInitials } from "../../../lib/initials";

const photoTypes = new Set(["image/jpeg", "image/png", "image/webp"]);

export function coachPhotoError(file: File | null) {
  if (!file) {
    return null;
  }
  return photoTypes.has(file.type) && file.size > 0 && file.size <= 2_097_152 ? null : "Choose a JPEG, PNG, or WebP image under 2 MB.";
}

export async function uploadCoachPhoto(trainerId: string, file: File) {
  const body = new FormData();
  body.append("file", file);
  const session = getSession();
  const response = await fetch(`${apiRoot()}/trainers/${trainerId}/profile-photo`, {
    method: "POST",
    headers: session ? { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" } : { Accept: "application/json" },
    body,
  });
  if (!response.ok) {
    const detail = await response.text();
    const message = /"detail"\s*:\s*"([^"]+)"/.exec(detail)?.[1];
    throw new ApiError(message ?? "The coach was saved, but the photo was not.", response.status);
  }
}

export function CoachPhotoField({ firstName, lastName, file, existing, removed, onFile, onRemove }: { firstName: string; lastName: string; file: File | null; existing: Blob | null; removed: boolean; onFile: (file: File | null, message: string | null) => void; onRemove: () => void }) {
  const preview = file ?? (removed ? null : existing);
  return (
    <div className="flex items-center gap-4">
      <PhotoFace file={preview} initials={nameInitials(firstName, lastName)} />
      <div className="grid gap-2">
        <label className="grid gap-2">Profile photo
          <input accept="image/jpeg,image/png,image/webp" aria-label="Profile photo" className="field" onChange={(event) => { const next = event.target.files?.[0] ?? null; onFile(next, coachPhotoError(next)); }} type="file" />
        </label>
        {preview ? <button className="min-h-11 justify-self-start rounded-full border border-line px-4" onClick={onRemove} type="button">Remove photo</button> : null}
      </div>
    </div>
  );
}

function PhotoFace({ file, initials }: { file: Blob | null; initials: string }) {
  const src = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);
  useEffect(() => () => { if (src) URL.revokeObjectURL(src); }, [src]);
  if (!src) {
    return <span className="inline-flex h-20 w-20 items-center justify-center rounded-full bg-background text-lg font-semibold">{initials}</span>;
  }
  return <img alt="" className="h-20 w-20 rounded-full object-cover" src={src} />;
}
