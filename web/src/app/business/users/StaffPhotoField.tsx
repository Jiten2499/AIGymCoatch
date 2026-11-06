"use client";

import { useEffect, useMemo, useState } from "react";
import { apiRoot, ApiError } from "../../../lib/api";
import { nameInitials } from "../../../lib/initials";
import { getSession } from "../../../lib/session";

const photoTypes = new Set(["image/jpeg", "image/png", "image/webp"]);

export function staffPhotoError(file: File | null) {
  if (!file) {
    return null;
  }

  return photoTypes.has(file.type) && file.size > 0 && file.size <= 2_097_152 ? null : "Choose a JPEG, PNG, or WebP image under 2 MB.";
}

export async function uploadStaffPhoto(userId: string, file: File) {
  const body = new FormData();
  body.append("file", file);
  const session = getSession();
  const response = await fetch(`${apiRoot()}/business/users/${userId}/photo`, {
    method: "POST",
    headers: session ? { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" } : { Accept: "application/json" },
    body,
  });
  if (!response.ok) {
    const detail = await response.text();
    const message = /"detail"\s*:\s*"([^"]+)"/.exec(detail)?.[1];
    throw new ApiError(message ?? "The staff member was created, but the photo was not.", response.status);
  }
}

export function StaffPhotoField({ firstName, lastName, onFile }: { firstName: string; lastName: string; onFile: (file: File | null, message: string | null) => void }) {
  const [file, setFile] = useState<File | null>(null);
  return (
    <div className="flex items-center gap-4">
      <PhotoFace file={file} initials={nameInitials(firstName, lastName)} />
      <label className="grid flex-1 gap-2">
        Profile photo
        <input
          accept="image/jpeg,image/png,image/webp"
          aria-label="Profile photo"
          className="field"
          onChange={(event) => {
            const next = event.target.files?.[0] ?? null;
            setFile(next);
            onFile(next, staffPhotoError(next));
          }}
          type="file"
        />
      </label>
    </div>
  );
}

function PhotoFace({ file, initials }: { file: File | null; initials: string }) {
  const src = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);
  useEffect(() => () => { if (src) URL.revokeObjectURL(src); }, [src]);
  if (!src) {
    return <span className="inline-flex h-16 w-16 items-center justify-center rounded-full bg-background text-sm font-semibold">{initials}</span>;
  }

  return <img alt="" className="h-16 w-16 rounded-full object-cover" src={src} />;
}
