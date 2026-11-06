"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { DataTable } from "../../../components/DataTable";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { CoachMonthlyTargets, type CoachTarget } from "./CoachTargets";

type RewardTransaction = {
  id: string;
  points: number;
  transactionType: string;
  approvalStatus: string;
  reason: string | null;
  createdAt: string;
  postedAt: string | null;
  sourceAssignmentId: string | null;
};

type RewardDefinition = { id: string; name: string; status: string; defaultAwardPoints: number; defaultGoal: number; metric: string; period: string };
type RewardAssignment = {
  id: string;
  snapshotName: string;
  snapshotMetric: string;
  goalValue: number;
  snapshotAwardPoints: number;
  period: string;
  effectiveStart: string;
  effectiveEnd: string | null;
  status: string;
};

type CreditRow = {
  id: string;
  target: string;
  metric: string;
  goal: string;
  points: string;
  creditType: string;
  status: string;
  reason: string;
  start: string;
  end: string;
  posted: string;
  canAward: boolean;
};

function formatWhen(value: string | null | undefined) {
  if (!value) {
    return "—";
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function relatedCredit(assignmentId: string, transactions: RewardTransaction[]) {
  const linked = transactions
    .filter((item) => item.sourceAssignmentId === assignmentId)
    .sort((left, right) => right.createdAt.localeCompare(left.createdAt));
  return linked.find((item) => item.transactionType === "Award")
    ?? linked.find((item) => item.transactionType !== "Reversal")
    ?? linked[0]
    ?? null;
}

function buildRows(assignments: RewardAssignment[], transactions: RewardTransaction[]): CreditRow[] {
  return [...assignments]
    .sort((left, right) => right.effectiveStart.localeCompare(left.effectiveStart))
    .map((item) => {
      const credit = relatedCredit(item.id, transactions);
      return {
        id: item.id,
        target: item.snapshotName,
        metric: item.snapshotMetric,
        goal: String(item.goalValue),
        points: credit ? String(credit.points) : String(item.snapshotAwardPoints),
        creditType: credit?.transactionType ?? "—",
        status: credit?.approvalStatus ?? item.status,
        reason: credit?.reason || "—",
        start: item.effectiveStart,
        end: item.effectiveEnd || "—",
        posted: formatWhen(credit?.postedAt),
        canAward: false,
      };
    });
}

export function CoachCredits({
  coachId,
  targets,
  range,
  onTargetsChanged,
}: {
  coachId: string;
  targets: CoachTarget[];
  range: { from: string; to: string };
  onTargetsChanged: () => void;
}) {
  const client = useQueryClient();
  const [assignOpen, setAssignOpen] = useState(false);
  const assignments = useQuery({
    queryKey: ["coach-reward-assignments", coachId],
    queryFn: () => api<RewardAssignment[]>(`/trainers/reward-assignments?trainerId=${coachId}`),
  });
  const transactions = useQuery({
    queryKey: ["coach-reward-transactions", coachId],
    queryFn: () => api<RewardTransaction[]>(`/trainers/${coachId}/reward-transactions`),
  });
  const refresh = async () => {
    await client.invalidateQueries({ queryKey: ["coach-reward-assignments", coachId] });
    await client.invalidateQueries({ queryKey: ["coach-reward-transactions", coachId] });
    await client.invalidateQueries({ queryKey: ["coach-reward-balance", coachId] });
  };
  const award = useMutation({
    mutationFn: (assignmentId: string) => api(`/trainers/reward-assignments/${assignmentId}/award`, {
      method: "POST",
      body: JSON.stringify({
        points: null,
        idempotencyKey: `award:${assignmentId}:${crypto.randomUUID()}`,
        reason: "Target recognition",
      }),
    }),
    onSuccess: () => void refresh(),
  });
  const rows = useMemo(
    () => buildRows(assignments.data ?? [], transactions.data ?? []),
    [assignments.data, transactions.data],
  );
  const loading = assignments.isLoading || transactions.isLoading;
  const error = [assignments.error, transactions.error, award.error].find((item) => item instanceof ApiError) as ApiError | undefined;

  return (
    <div className="mt-4 grid min-w-0 gap-4">
      <CoachMonthlyTargets coachId={coachId} onChanged={onTargetsChanged} range={range} targets={targets} />
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h3 className="text-base font-semibold">Coach credit assignments</h3>
          <p className="mt-1 text-sm text-muted">Reward definitions, earned credits and approval status.</p>
        </div>
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAssignOpen(true)} type="button">Assign target</button>
      </div>
      {loading ? <p className="text-sm" role="status">Loading targets and credits.</p> : null}
      {error ? <p className="text-sm" role="alert">{error.message}</p> : null}
      {!loading ? (
        <DataTable
          columns={[
            { header: "Target", cell: (row) => row.target, value: (row) => row.target },
            { header: "Metric", cell: (row) => row.metric, value: (row) => row.metric },
            { header: "Goal", cell: (row) => row.goal, value: (row) => row.goal },
            { header: "Points", cell: (row) => row.points, value: (row) => row.points },
            { header: "Credit type", cell: (row) => row.creditType, value: (row) => row.creditType },
            { header: "Status", cell: (row) => row.status, value: (row) => row.status },
            { header: "Reason", cell: (row) => row.reason, value: (row) => row.reason },
            { header: "Start", cell: (row) => row.start, value: (row) => row.start },
            { header: "End", cell: (row) => row.end, value: (row) => row.end },
            { header: "Posted", cell: (row) => row.posted, value: (row) => row.posted },
            {
              header: "Actions",
              cell: (row) => row.canAward ? (
                <button className="min-h-11 rounded-full border border-line px-3" disabled={award.isPending} onClick={() => award.mutate(row.id)} type="button">Award points</button>
              ) : "—",
              value: () => "",
            },
          ]}
          empty="No assigned targets yet."
          rowKey={(row) => row.id}
          rows={rows}
        />
      ) : null}
      {assignOpen ? (
        <AssignTargetDialog
          coachId={coachId}
          onCancel={() => setAssignOpen(false)}
          onSaved={async () => {
            setAssignOpen(false);
            await refresh();
          }}
        />
      ) : null}
    </div>
  );
}

function AssignTargetDialog({ coachId, onCancel, onSaved }: { coachId: string; onCancel: () => void; onSaved: () => Promise<void> }) {
  const today = new Date().toISOString().slice(0, 10);
  const [definitionId, setDefinitionId] = useState("");
  const [goal, setGoal] = useState("");
  const [startDate, setStartDate] = useState(today);
  const [endDate, setEndDate] = useState("");
  const definitions = useQuery({
    queryKey: ["trainer-target-definitions", "coach"],
    queryFn: () => api<RewardDefinition[]>("/configuration/trainer-target-definitions"),
  });
  const assign = useMutation({
    mutationFn: () => api(`/trainers/${coachId}/reward-assignments`, {
      method: "POST",
      body: JSON.stringify({
        definitionId,
        goalValue: goal ? Number(goal) : null,
        effectiveStart: startDate,
        effectiveEnd: endDate || null,
      }),
    }),
    onSuccess: () => void onSaved(),
  });
  const activeDefinitions = (definitions.data ?? []).filter((item) => item.status === "Active");

  return (
    <FormDialog onCancel={onCancel} title="Assign target">
      <form
        className="mt-4 grid gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          assign.mutate();
        }}
      >
        <FormField label="Target definition">
          <select aria-label="Target definition" className="field" onChange={(event) => setDefinitionId(event.target.value)} required value={definitionId}>
            <option value="">Select definition</option>
            {activeDefinitions.map((item) => (
              <option key={item.id} value={item.id}>{item.name} · {item.defaultAwardPoints} pts</option>
            ))}
          </select>
        </FormField>
        <FormField label="Goal">
          <input aria-label="Goal override" className="field" onChange={(event) => setGoal(event.target.value)} placeholder="Leave blank for definition default" value={goal} />
        </FormField>
        <FormField label="Start date">
          <input aria-label="Target start date" className="field" onChange={(event) => setStartDate(event.target.value)} required type="date" value={startDate} />
        </FormField>
        <FormField label="End date">
          <input aria-label="Target end date" className="field" onChange={(event) => setEndDate(event.target.value)} type="date" value={endDate} />
        </FormField>
        {activeDefinitions.length === 0 ? <p className="text-sm text-muted">Add an active definition under Configuration → Coach Rewards.</p> : null}
        {definitions.error instanceof ApiError ? <p role="alert">{definitions.error.message}</p> : null}
        {assign.error instanceof ApiError ? <p role="alert">{assign.error.message}</p> : null}
        <FormActions cancel={onCancel} pending={assign.isPending} submitLabel="Assign target" />
      </form>
    </FormDialog>
  );
}
