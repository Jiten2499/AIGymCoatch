"use client";

import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { api, ApiError } from "../../../lib/api";
import type { AccessOverview } from "../../../lib/accessRoles";

export function AccessOverviewPanel({ tenantId }: { tenantId: string | null }) {
  const [search, setSearch] = useState("");
  const overview = useQuery({
    queryKey: ["access-overview", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<AccessOverview[]>("/access-roles/overview"),
  });
  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (overview.data ?? []).filter((row) => !term || `${row.displayName} ${row.roleLabel} ${row.permissions.join(" ")} ${row.restrictedModules.join(" ")}`.toLowerCase().includes(term));
  }, [overview.data, search]);

  return (
    <div className="mt-6">
      <p className="max-w-3xl text-muted">Review who can act in this business. Excessive access lists sensitive permissions. Restricted modules are areas where that person has no view permission.</p>
      <label className="mt-4 block max-w-md text-sm">Search
        <input className="field mt-1 w-full" onChange={(event) => setSearch(event.target.value)} value={search} />
      </label>
      {overview.isLoading ? <p className="mt-4">Loading access…</p> : null}
      {overview.error instanceof ApiError ? <p className="mt-4 text-red-700" role="alert">{overview.error.message}</p> : null}
      <div className="mt-4 grid gap-3">
        {rows.map((row) => (
          <article className="rounded-3xl border border-line p-4" key={row.userId}>
            <h3 className="font-semibold">{row.displayName}</h3>
            <p className="text-sm text-muted">{row.roleLabel} · member records: {row.memberScope}</p>
            <p className="mt-2 text-sm"><span className="font-semibold">Sensitive access:</span> {row.excessive.length === 0 ? "None" : row.excessive.join(", ")}</p>
            <p className="mt-1 text-sm"><span className="font-semibold">Restricted modules:</span> {row.restrictedModules.length === 0 ? "None" : row.restrictedModules.join(", ")}</p>
          </article>
        ))}
      </div>
    </div>
  );
}
