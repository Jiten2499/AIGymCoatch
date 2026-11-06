"use client";

import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { apiBlob } from "../lib/api";
import { nameInitials } from "../lib/initials";

export function UserAvatar({
  userId,
  firstName,
  lastName,
  hasPhoto = false,
  size = 40,
}: {
  userId?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  hasPhoto?: boolean;
  size?: number;
}) {
  const initials = nameInitials(firstName, lastName);
  const photo = useQuery({
    queryKey: ["user-photo", userId],
    enabled: hasPhoto === true && Boolean(userId),
    retry: false,
    queryFn: async () => {
      const blob = await apiBlob(`/users/${userId}/photo`);
      return blob ? URL.createObjectURL(blob) : null;
    },
  });

  useEffect(() => {
    const url = photo.data;
    return () => {
      if (url) {
        URL.revokeObjectURL(url);
      }
    };
  }, [photo.data]);

  const label = `${firstName ?? ""} ${lastName ?? ""}`.trim() || "User";
  if (photo.data) {
    return <img alt={label} className="rounded-full object-cover" height={size} src={photo.data} width={size} />;
  }

  return (
    <span aria-label={label} className="inline-flex items-center justify-center rounded-full bg-surface text-xs font-semibold" style={{ height: size, width: size }}>
      {initials}
    </span>
  );
}
