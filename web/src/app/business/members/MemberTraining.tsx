"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { ScrollableGrid } from "../../../components/ScrollableGrid";
import { api, ApiError } from "../../../lib/api";
import type { GymMembership } from "../../../lib/gym";
import { ptPackageLabel, type PtPackageOption } from "../../../lib/ptPackage";
import type { Coach, Page } from "../../../lib/types";
import { PtPackagePaymentDialog } from "./PtPackagePaymentDialog";

type Assignment = {
  id: string;
  packageName: string;
  trainerId: string | null;
  startDate: string;
  endDate: string;
  sessionCount: number;
  sessionsRemaining: number;
  price: number;
  currency: string;
  status: string;
  paymentStatus: string;
  amountPaid?: number;
  paymentMethod?: string | null;
  paymentDate?: string | null;
};

type Session = {
  id: string;
  trainerId: string;
  scheduledStartUtc: string;
  completedOn: string | null;
  status: string;
  notes: string | null;
  consumptionId: string | null;
  consumptionStatus: string | null;
};

export function MemberTraining({
  canAssign,
  canRecord,
  memberId,
  tenantId,
  trainerId,
}: {
  canAssign: boolean;
  canRecord: boolean;
  memberId: string;
  tenantId: string | null;
  trainerId: string | null;
}) {
  const client = useQueryClient();
  const assignments = useQuery({
    queryKey: ["member-pt", tenantId, memberId],
    enabled: tenantId !== null,
    queryFn: () => api<Assignment[]>(`/members/${memberId}/pt-assignments`),
  });
  const sessions = useQuery({
    queryKey: ["member-pt-sessions", tenantId, memberId],
    enabled: tenantId !== null,
    queryFn: () => api<Session[]>(`/members/${memberId}/pt-sessions`),
  });
  const trainers = useQuery({
    queryKey: ["trainers", tenantId, "member-detail"],
    enabled: tenantId !== null && (canAssign || canRecord),
    queryFn: () => api<Page<Coach>>("/trainers?page=1&pageSize=50"),
  });
  const refresh = async () => {
    await client.invalidateQueries({ queryKey: ["member-pt", tenantId, memberId] });
    await client.invalidateQueries({ queryKey: ["member-pt-sessions", tenantId, memberId] });
    await client.invalidateQueries({ queryKey: ["memberships", tenantId, memberId] });
    await client.invalidateQueries({ queryKey: ["ledger-summary", tenantId] });
    await client.invalidateQueries({ queryKey: ["ledger-transactions", tenantId] });
    await client.invalidateQueries({ queryKey: ["financial-accounts", tenantId] });
  };
  const names = new Map((trainers.data?.items ?? []).map((trainer) => [trainer.id, `${trainer.firstName} ${trainer.lastName}`]));
  const name = (id: string | null) => (id ? names.get(id) ?? "Trainer" : "—");

  return (
    <section className="mt-6 grid gap-4">
      {assignments.error instanceof ApiError ? <p role="alert">{assignments.error.message}</p> : null}
      {canAssign ? <AssignForm memberId={memberId} onAssigned={refresh} trainerId={trainerId} trainers={trainers.data?.items ?? []} /> : null}
      <ScrollableGrid>
      <div className="grid w-max min-w-full gap-3">
        {(assignments.data ?? []).map((item) => (
          <article className="rounded-2xl border border-line bg-surface p-4 text-sm" key={item.id}>
            <h2 className="font-semibold">{item.packageName}</h2>
            <p className="mt-1 text-muted">Valid {item.startDate} to {item.endDate} · {item.status} · {item.paymentStatus}</p>
            <p className="mt-1">Sessions remaining {item.sessionsRemaining} of {item.sessionCount} · {name(item.trainerId)} · {item.price} {item.currency}{item.amountPaid ? ` · Paid ${item.amountPaid}${item.paymentMethod ? ` by ${item.paymentMethod}` : ""}${item.paymentDate ? ` on ${item.paymentDate}` : ""}` : ""}</p>
          </article>
        ))}
        {assignments.data && assignments.data.length === 0 ? <p className="text-muted">No personal-training package is assigned.</p> : null}
      </div>
      </ScrollableGrid>
      {canRecord ? <RecordForm assignments={assignments.data ?? []} memberId={memberId} onRecorded={refresh} trainerId={trainerId} trainers={trainers.data?.items ?? []} /> : null}
      <h2 className="font-semibold">Session history</h2>
      {sessions.error instanceof ApiError ? <p role="alert">{sessions.error.message}</p> : null}
      <ScrollableGrid>
      <ul className="grid w-max min-w-full gap-2 text-sm">
        {(sessions.data ?? []).map((item) => (
          <li className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-line bg-surface p-3" key={item.id}>
            <span>{item.completedOn || item.scheduledStartUtc} · {name(item.trainerId)} · {item.status}{item.notes ? ` · ${item.notes}` : ""}</span>
            {canRecord && item.consumptionId && item.consumptionStatus === "Consumed" ? <ReverseButton consumptionId={item.consumptionId} memberId={memberId} onReversed={refresh} /> : null}
          </li>
        ))}
        {sessions.data && sessions.data.length === 0 ? <li className="text-muted">No sessions recorded.</li> : null}
      </ul>
      </ScrollableGrid>
    </section>
  );
}

function AssignForm({ memberId, onAssigned, trainerId, trainers }: { memberId: string; onAssigned: () => Promise<void>; trainerId: string | null; trainers: Coach[] }) {
  const packages = useQuery({ queryKey: ["pt-packages", "member-detail"], queryFn: () => api<PtPackageOption[]>("/pt-packages?activeOnly=true") });
  const [packageId, setPackageId] = useState("");
  const [selectedTrainer, setSelectedTrainer] = useState(trainerId ?? "");
  const [start, setStart] = useState(new Date().toISOString().slice(0, 10));
  const [paying, setPaying] = useState(false);
  const selectedPackage = (packages.data ?? []).find((item) => item.id === packageId);
  return (
    <form className="grid gap-3 rounded-2xl border border-line bg-surface p-4" onSubmit={(event) => { event.preventDefault(); setPaying(true); }}>
      <h2 className="font-semibold">Assign PT package</h2>
      <select aria-label="PT package" className="field" onChange={(event) => setPackageId(event.target.value)} required value={packageId}>
        <option value="">Select a package</option>
        {(packages.data ?? []).map((item) => <option key={item.id} value={item.id}>{ptPackageLabel(item)}</option>)}
      </select>
      <select aria-label="PT trainer" className="field" onChange={(event) => setSelectedTrainer(event.target.value)} value={selectedTrainer}>
        <option value="">No trainer</option>
        {trainers.map((trainer) => <option key={trainer.id} value={trainer.id}>{trainer.firstName} {trainer.lastName}</option>)}
      </select>
      <input aria-label="Package start date" className="field" onChange={(event) => setStart(event.target.value)} required type="date" value={start} />
      {packages.error instanceof ApiError ? <p role="alert">{packages.error.message}</p> : null}
      <button className="min-h-11 w-fit rounded-full bg-accent px-4 font-semibold text-background" type="submit">Assign package</button>
      {paying && selectedPackage ? (
        <PtPackagePaymentDialog
          currency={selectedPackage.currency}
          memberId={memberId}
          onCancel={() => setPaying(false)}
          onSaved={async () => { setPaying(false); await onAssigned(); }}
          packageId={packageId}
          packageName={selectedPackage.name}
          price={selectedPackage.price}
          startDate={start}
          trainerId={selectedTrainer || null}
        />
      ) : null}
    </form>
  );
}

function RecordForm({ assignments, memberId, onRecorded, trainerId, trainers }: { assignments: Assignment[]; memberId: string; onRecorded: () => Promise<void>; trainerId: string | null; trainers: Coach[] }) {
  const memberships = useQuery({ queryKey: ["memberships", memberId, "pt"], queryFn: () => api<Page<GymMembership>>(`/memberships?memberId=${memberId}&page=1&pageSize=20`) });
  const [source, setSource] = useState("");
  const [selectedTrainer, setSelectedTrainer] = useState(trainerId ?? trainers[0]?.id ?? "");
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const record = useMutation({
    mutationFn: () => {
      const [kind, id] = source.split(":");
      return api(`/members/${memberId}/pt-sessions`, {
        method: "POST",
        body: JSON.stringify({ trainerId: selectedTrainer, completedOn: date, membershipId: kind === "membership" ? id : null, assignmentId: kind === "assignment" ? id : null, sessionId: null }),
      });
    },
    onSuccess: onRecorded,
  });
  const choices = [
    ...assignments.filter((item) => item.sessionsRemaining > 0).map((item) => ({ value: `assignment:${item.id}`, label: `${item.packageName} · ${item.sessionsRemaining} left` })),
    ...(memberships.data?.items ?? []).filter((item) => (item.ptSessionsRemaining ?? 0) > 0).map((item) => ({ value: `membership:${item.id}`, label: `${item.planName || item.membershipNumber} · ${item.ptSessionsRemaining} included left` })),
  ];
  return (
    <form className="grid gap-3 rounded-2xl border border-line bg-surface p-4" onSubmit={(event) => { event.preventDefault(); record.mutate(); }}>
      <h2 className="font-semibold">Record session</h2>
      <select aria-label="Session entitlement" className="field" onChange={(event) => setSource(event.target.value)} required value={source}>
        <option value="">Select a package or included membership</option>
        {choices.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
      </select>
      <select aria-label="Session trainer" className="field" onChange={(event) => setSelectedTrainer(event.target.value)} required value={selectedTrainer}>
        <option value="">Select a trainer</option>
        {trainers.map((trainer) => <option key={trainer.id} value={trainer.id}>{trainer.firstName} {trainer.lastName}</option>)}
      </select>
      <input aria-label="Session date" className="field" onChange={(event) => setDate(event.target.value)} required type="date" value={date} />
      {record.error instanceof ApiError ? <p role="alert">{record.error.message}</p> : null}
      <button className="min-h-11 w-fit rounded-full bg-accent px-4 font-semibold text-background" disabled={choices.length === 0} type="submit">Record session</button>
    </form>
  );
}

function ReverseButton({ consumptionId, memberId, onReversed }: { consumptionId: string; memberId: string; onReversed: () => Promise<void> }) {
  const reverse = useMutation({
    mutationFn: () => api(`/members/${memberId}/pt-sessions/${consumptionId}/reverse`, { method: "POST", body: JSON.stringify({ reason: "Corrected by tenant admin" }) }),
    onSuccess: onReversed,
  });
  return (
    <button className="min-h-11 rounded-full border border-line px-3" onClick={() => reverse.mutate()} type="button">
      {reverse.isPending ? "Restoring..." : "Restore session"}
    </button>
  );
}
