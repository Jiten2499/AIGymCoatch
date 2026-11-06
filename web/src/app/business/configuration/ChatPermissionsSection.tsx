"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { api, ApiError } from "../../../lib/api";

type ChatRule = {
  code: string;
  enabled: boolean;
  scope: "AssignedOnly" | "SameTenant";
  allowImages: boolean;
  allowDocuments: boolean;
};

const labels: Record<string, string> = {
  MemberAssignedTrainer: "Member ↔ assigned trainer",
  MemberMember: "Member ↔ member",
  TrainerAssignedMember: "Trainer ↔ assigned member",
  TrainerUnassignedMember: "Trainer ↔ non-assigned member",
  TrainerTrainer: "Trainer ↔ trainer",
  StaffStaff: "Staff ↔ staff",
  StaffTrainer: "Staff ↔ trainer",
  TrainerStaff: "Trainer ↔ staff",
  StaffMember: "Staff ↔ member",
  AdminAll: "Admin ↔ all active users",
};

export function ChatPermissionsSection() {
  const client = useQueryClient();
  const rules = useQuery({
    queryKey: ["chat-permissions"],
    queryFn: () => api<ChatRule[]>("/chat/permissions"),
  });
  const [draft, setDraft] = useState<ChatRule[] | null>(null);
  const rows = draft ?? rules.data ?? [];
  const save = useMutation({
    mutationFn: () => api<ChatRule[]>("/chat/permissions", { method: "PUT", body: JSON.stringify({ rules: rows }) }),
    onSuccess: async (next) => {
      setDraft(null);
      client.setQueryData(["chat-permissions"], next);
    },
  });

  function update(code: string, patch: Partial<ChatRule>) {
    setDraft(rows.map((row) => row.code === code ? { ...row, ...patch } : row));
  }

  return (
    <section className="mt-8">
      <h2 className="text-xl font-semibold">Chat & Messaging</h2>
      <p className="mt-2 max-w-3xl text-muted">Choose who can start a one-to-one chat in this gym. These rules are enforced by the API. An admin who can message everyone still cannot open other people&apos;s private conversations.</p>
      {rules.isLoading ? <p className="mt-4">Loading chat rules…</p> : null}
      {rules.error instanceof ApiError ? <p className="mt-4 text-red-700">{rules.error.message}</p> : null}
      <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[720px] text-left">
          <thead>
            <tr className="border-b border-line text-sm text-muted">
              <th className="py-3">Relationship</th>
              <th>Enabled</th>
              <th>Scope</th>
              <th>Images</th>
              <th>Documents</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr className="border-b border-line" key={row.code}>
                <td className="py-3">{labels[row.code] ?? row.code}</td>
                <td><input aria-label={`${labels[row.code] ?? row.code} enabled`} checked={row.enabled} onChange={(event) => update(row.code, { enabled: event.target.checked })} type="checkbox" /></td>
                <td>
                  <select aria-label={`${labels[row.code] ?? row.code} scope`} className="min-h-11 rounded-full border border-line px-3" onChange={(event) => update(row.code, { scope: event.target.value as ChatRule["scope"] })} value={row.scope}>
                    <option value="AssignedOnly">Assigned only</option>
                    <option value="SameTenant">Same gym</option>
                  </select>
                </td>
                <td><input aria-label={`${row.code} images`} checked={row.allowImages} onChange={(event) => update(row.code, { allowImages: event.target.checked })} type="checkbox" /></td>
                <td><input aria-label={`${row.code} documents`} checked={row.allowDocuments} onChange={(event) => update(row.code, { allowDocuments: event.target.checked })} type="checkbox" /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h3 className="mt-6 font-semibold">Effective matrix</h3>
      <ul className="mt-2 space-y-1 text-sm">
        {rows.map((row) => (
          <li key={row.code}>{labels[row.code] ?? row.code}: {row.enabled ? "allowed" : "blocked"} · {row.scope === "AssignedOnly" ? "assigned people" : "anyone in this gym"} · {row.allowImages ? "images" : "no images"} · {row.allowDocuments ? "documents" : "no documents"}</li>
        ))}
      </ul>
      <button className="mt-4 min-h-11 rounded-full bg-accent px-4 font-semibold text-background" disabled={save.isPending || rows.length === 0} onClick={() => save.mutate()} type="button">Save chat rules</button>
      {save.error instanceof ApiError ? <p className="mt-2 text-red-700">{save.error.message}</p> : null}
    </section>
  );
}
