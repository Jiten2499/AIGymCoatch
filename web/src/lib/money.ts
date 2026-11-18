"use client";

import { useQuery } from "@tanstack/react-query";
import { useSyncExternalStore } from "react";
import { api } from "./api";
import { getSession, subscribeSession } from "./session";

export function formatAmount(amount: number, currency: string): string {
  const code = currency.trim().toUpperCase() || "USD";
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency: code, maximumFractionDigits: 2 }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${code}`;
  }
}

export function useWorkspaceCurrency(): string {
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  const profile = useQuery({
    queryKey: ["business-profile", tenantId, "currency"],
    enabled: tenantId !== null,
    queryFn: () => api<{ currency: string }>("/business/profile"),
  });
  const code = profile.data?.currency?.trim().toUpperCase();
  return code && code.length === 3 ? code : "USD";
}
