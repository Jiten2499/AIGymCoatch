"use client";

import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { apiBlob } from "../../../lib/api";
import { nameInitials } from "../../../lib/initials";

export function ChatAvatar({ name, userId, hasPhoto }: { name: string; userId: string; hasPhoto?: boolean }) {
  const photo = useQuery({
    queryKey: ["chat-photo", userId],
    enabled: hasPhoto === true && Boolean(userId),
    retry: false,
    queryFn: () => apiBlob(`/chat/people/${userId}/photo`),
  });
  const [preview, setPreview] = useState<string | null>(null);

  useEffect(() => {
    const blob = photo.data;
    if (!blob) {
      setPreview(null);
      return undefined;
    }

    const url = URL.createObjectURL(blob);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [photo.data]);

  if (preview) {
    return <img alt="" className="h-11 w-11 shrink-0 rounded-full object-cover" src={preview} />;
  }

  return (
    <span aria-hidden="true" className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-accent text-sm font-semibold text-background">
      {nameInitials(name)}
    </span>
  );
}
