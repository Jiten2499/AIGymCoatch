"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { DataTable } from "../../components/DataTable";
import { FormDialog } from "../../components/FormDialog";
import { PageState } from "../../components/AdminShell";
import { api, ApiError } from "../../lib/api";
import { monthBounds } from "../../lib/trainerPeriod";
import { getSession, subscribeSession } from "../../lib/session";
import { CoachDetails } from "./coaches/CoachDetails";

type TrainerRow = {
  id: string;
  name: string;
  status: string;
  specialization: string | null;
  assignedMembers: number;
  activePtClients: number;
  joiningDate: string | null;
  employeeCode: string | null;
  commissionEligible: boolean;
};

type Candidate = { userId: string; name: string; email: string };
type Assignment = { id: string; memberId: string; memberName: string; startDate: string; endDate: string | null; status: string };
type Target = { id: string; targetType: string; period: string; periodStart: string; periodEnd: string; targetValue: number; actual: number; achievementPercent: number | null };
type Incentive = { targetType: string; percent: number | null; status: string; calculatedAmount: number | null };
type Details = TrainerRow & { bio: string | null; members: Assignment[]; targets: Target[]; incentives: Incentive[] };

export function TrainerPanel() {
  const client = useQueryClient();
  const tenantId = useSyncExternalStore(subscribeSession, () => getSession()?.currentTenant?.id ?? null, () => null);
  const month = new Date().toISOString().slice(0, 7);
  const range = monthBounds(month);
  const trainers = useQuery({ queryKey: ["trainer-workspace", tenantId], queryFn: () => api<TrainerRow[]>("/trainers/workspace") });
  const [selected, setSelected] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState(false);
  const details = useQuery({
    queryKey: ["trainer-workspace", tenantId, selected, range.from],
    enabled: selected !== null,
    queryFn: () => api<Details>(`/trainers/${selected}/workspace?from=${range.from}&to=${range.to}`),
  });
  const denied = trainers.error instanceof ApiError && trainers.error.status === 403;

  return (
    <PageState error={denied ? "You do not have permission to view trainers." : trainers.error instanceof ApiError ? trainers.error.message : null} loading={trainers.isLoading} onRetry={() => void trainers.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-3xl font-semibold">Trainers</h1>
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAdding(true)} type="button">Add trainer</button>
      </div>
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Trainer", cell: (row) => <button className="min-h-11 font-semibold" onClick={() => setSelected(row.id)} type="button">{row.name}</button> },
            { header: "Status", cell: (row) => row.status },
            { header: "Specialization", cell: (row) => row.specialization || "—" },
            { header: "Assigned Members", cell: (row) => String(row.assignedMembers) },
            { header: "Active PT Clients", cell: (row) => String(row.activePtClients) },
            { header: "Joining Date", cell: (row) => row.joiningDate || "—" },
            { header: "Actions", cell: (row) => <span className="flex gap-2"><button className="min-h-11" onClick={() => { setSelected(row.id); setEditing(true); }} type="button">Edit</button><button className="min-h-11" onClick={() => setSelected(row.id)} type="button">View details</button></span> },
          ]}
          empty="No trainers are set up for this business."
          rowKey={(row) => row.id}
          rows={trainers.data ?? []}
        />
      </div>
      {adding ? <TrainerForm title="Add trainer" onClose={() => setAdding(false)} onSaved={async () => { setAdding(false); await client.invalidateQueries({ queryKey: ["trainer-workspace", tenantId] }); }} /> : null}
      {editing && details.data ? <TrainerForm title="Edit trainer" trainer={details.data} onClose={() => setEditing(false)} onSaved={async () => { setEditing(false); await client.invalidateQueries({ queryKey: ["trainer-workspace", tenantId] }); }} /> : null}
      {selected && !editing ? <CoachDetails onClose={() => setSelected(null)} trainerId={selected} /> : null}
    </PageState>
  );
}

function TrainerForm({ title, trainer, onClose, onSaved }: { title: string; trainer?: Details; onClose: () => void; onSaved: () => Promise<void> }) {
  const candidates = useQuery({ queryKey: ["trainer-candidates"], enabled: !trainer, queryFn: () => api<Candidate[]>("/trainers/candidates") });
  const [userId, setUserId] = useState("");
  const [employeeCode, setEmployeeCode] = useState(trainer?.employeeCode ?? "");
  const [specialization, setSpecialization] = useState(trainer?.specialization ?? "");
  const [bio, setBio] = useState(trainer?.bio ?? "");
  const [joiningDate, setJoiningDate] = useState(trainer?.joiningDate ?? "");
  const [commissionEligible, setCommissionEligible] = useState(trainer?.commissionEligible ?? false);
  const [status, setStatus] = useState(trainer?.status ?? "Active");
  const save = useMutation({
    mutationFn: () => trainer
      ? api(`/trainers/${trainer.id}/employment`, { method: "PUT", body: JSON.stringify({ employeeCode, specialization, bio, joiningDate: joiningDate || null, commissionEligible, status }) })
      : api("/trainers/from-user", { method: "POST", body: JSON.stringify({ userId, employeeCode, specialization, bio, joiningDate: joiningDate || null, commissionEligible }) }),
    onSuccess: () => void onSaved(),
  });
  return (
    <FormDialog onCancel={onClose} title={title} wide>
      <form className="mt-4 grid gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
      {trainer ? null : (
        <label className="grid gap-1">Existing staff user
          <select className="field" onChange={(event) => setUserId(event.target.value)} required value={userId}>
            <option value="">Select a user</option>
            {(candidates.data ?? []).map((candidate) => <option key={candidate.userId} value={candidate.userId}>{candidate.name} · {candidate.email}</option>)}
          </select>
        </label>
      )}
      <label className="grid gap-1">Employee code<input className="field" onChange={(event) => setEmployeeCode(event.target.value)} value={employeeCode} /></label>
      <label className="grid gap-1">Specialization<input className="field" onChange={(event) => setSpecialization(event.target.value)} value={specialization} /></label>
      <label className="grid gap-1">Bio<textarea className="field" onChange={(event) => setBio(event.target.value)} value={bio} /></label>
      <label className="grid gap-1">Joining date<input className="field" onChange={(event) => setJoiningDate(event.target.value)} type="date" value={joiningDate ?? ""} /></label>
      <label className="flex min-h-11 items-center gap-2"><input checked={commissionEligible} onChange={(event) => setCommissionEligible(event.target.checked)} type="checkbox" /> Commission eligible</label>
      {trainer ? <label className="grid gap-1">Status<select className="field" onChange={(event) => setStatus(event.target.value)} value={status}><option>Active</option><option>Inactive</option></select></label> : null}
      {save.error instanceof ApiError ? <p role="alert">{save.error.message}</p> : null}
      <div className="flex gap-2">
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" disabled={save.isPending} type="submit">Save</button>
        <button className="min-h-11" onClick={onClose} type="button">Cancel</button>
      </div>
    </form>
    </FormDialog>
  );
}

