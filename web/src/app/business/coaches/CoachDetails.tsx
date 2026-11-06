"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useState } from "react";
import { FormDialog } from "../../../components/FormDialog";
import { ProfileDeleteDialog } from "../../../components/ProfileDeleteDialog";
import { ScrollableGrid } from "../../../components/ScrollableGrid";
import { api, apiBlob, ApiError } from "../../../lib/api";
import { nameInitials } from "../../../lib/initials";
import { monthBounds } from "../../../lib/trainerPeriod";
import type { GymMember } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { memberDetailsHref } from "../members/memberAccess";
import { CoachCredits } from "./CoachCredits";
import { CoachHistory, type CoachHistoryItem } from "./CoachHistory";
import { CoachOverview, CoachTabIcon } from "./CoachOverview";
import { CoachPerformance } from "./CoachPerformance";

type Assignment = { id: string; memberId: string; memberName: string; phone: string | null; startDate: string; endDate: string | null; status: string; membershipStatus: string | null; membershipEnd: string | null };
type Target = { id: string; name: string | null; targetType: string; period: string; periodStart: string; periodEnd: string; targetValue: number; actual: number | null; achievementPercent: number | null; isActive: boolean };
type Rule = { id: string; name: string; kind: string; detail: string; start: string; end: string | null; active: boolean; requiresApproval: boolean };
type Calculation = { id: string; kind: string; ruleName: string; periodStart: string; periodEnd: string; amount: number; status: string; basis: string };
type Coach = {
  id: string; name: string; email: string; status: string; specialization: string | null; joiningDate: string | null; employeeCode: string | null;
  assignedMembers: number; activePtClients: number; newPtClients: number; achievementPercent: number | null;
  estimatedCommission: number; estimatedIncentive: number; pendingApproval: number; approvedAmount: number;
  ptSessionsCompleted?: number | null; ptPackagesSold?: number | null;
  assignments: Assignment[]; targets: Target[]; commissionRules: Rule[]; incentiveRules: Rule[]; calculations: Calculation[]; history: CoachHistoryItem[];
};
type TrainerRow = { id: string; name: string };

const tabs = [
  { id: "Overview", label: "Overview", icon: "overview" },
  { id: "Assigned Members", label: "Assigned Members", icon: "members" },
  { id: "Performance", label: "Performance", icon: "performance" },
  { id: "Target and Coach Credit", label: "Target and Coach Credit", icon: "target" },
  { id: "Commission & Incentives", label: "Commission & Incentives", icon: "commission" },
  { id: "History", label: "History", icon: "history" },
] as const;

export function CoachDetails({ trainerId, onClose, onDeleted }: { trainerId: string; onClose: () => void; onDeleted?: () => void }) {
  const client = useQueryClient();
  const [month, setMonth] = useState(new Date().toISOString().slice(0, 7));
  const [tab, setTab] = useState<(typeof tabs)[number]["id"]>("Overview");
  const [confirmDelete, setConfirmDelete] = useState(false);
  const remove = useMutation({
    mutationFn: () => api(`/trainers/${trainerId}`, { method: "DELETE" }),
    onSuccess: () => onDeleted?.(),
  });
  const range = monthBounds(month);
  const coach = useQuery({ queryKey: ["coach-workspace", trainerId, range.from], queryFn: () => api<Coach>(`/trainers/${trainerId}/coach?from=${range.from}&to=${range.to}`) });
  const photo = useQuery({
    queryKey: ["coach-photo", trainerId],
    retry: false,
    queryFn: () => apiBlob(`/trainers/${trainerId}/profile-photo`),
  });
  const [photoUrl, setPhotoUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!photo.data) {
      setPhotoUrl(null);
      return;
    }
    const url = URL.createObjectURL(photo.data);
    setPhotoUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [photo.data]);
  const refresh = () => void client.invalidateQueries({ queryKey: ["coach-workspace", trainerId] });
  const data = coach.data;
  const nameParts = (data?.name ?? "Coach").trim().split(/\s+/).filter(Boolean);
  const firstName = nameParts[0] ?? "Coach";
  const lastName = nameParts.slice(1).join(" ");
  const active = (data?.status ?? "").toLowerCase() === "active";

  return (
    <FormDialog
      large
      onCancel={onClose}
      title={data?.name ?? "Coach"}
      header={(
        <div className="flex items-start justify-between gap-4">
          <div className="flex min-w-0 items-center gap-4">
            {photoUrl ? (
              <img alt={data?.name ?? "Coach"} className="size-16 rounded-full object-cover ring-2 ring-line" height={64} src={photoUrl} width={64} />
            ) : (
              <span aria-hidden className="inline-flex size-16 items-center justify-center rounded-full bg-background text-lg font-semibold ring-2 ring-line">
                {nameInitials(firstName, lastName)}
              </span>
            )}
            <div className="min-w-0">
              <h2 className="truncate text-2xl font-semibold tracking-tight" id="form-dialog-title">{data?.name ?? (coach.isLoading ? "Loading coach…" : "Coach")}</h2>
              <div className="mt-2 flex flex-wrap items-center gap-2">
                {data ? (
                  <span className={`inline-flex min-h-8 items-center rounded-full px-3 text-sm font-semibold ${active ? "bg-accent/15 text-accent" : "border border-line text-muted"}`}>
                    {active ? "Active" : data.status || "Inactive"}
                  </span>
                ) : null}
                {data?.specialization ? <span className="truncate text-sm text-muted">{data.specialization}</span> : null}
              </div>
            </div>
          </div>
          <div className="flex shrink-0 gap-2"><button className="min-h-11 rounded-full border border-line px-4" onClick={() => setConfirmDelete(true)} type="button">Delete</button><button className="min-h-11 rounded-full border border-line px-4" onClick={onClose} type="button">Close</button></div>
        </div>
      )}
    >
      {confirmDelete ? (
        <ProfileDeleteDialog
          error={remove.error instanceof ApiError ? remove.error.message : null}
          name={data?.name ?? "this coach"}
          onCancel={() => { setConfirmDelete(false); remove.reset(); }}
          onConfirm={() => remove.mutate()}
          pending={remove.isPending}
          title="Delete coach"
        />
      ) : null}
      <div className="-mx-1 overflow-x-auto pb-1" role="tablist">
        <div className="flex min-w-max gap-2 px-1">
          {tabs.map((item) => {
            const selected = tab === item.id;
            return (
              <button
                aria-selected={selected}
                className={`inline-flex min-h-11 items-center gap-2 rounded-full px-3 text-sm whitespace-nowrap ${selected ? "bg-accent font-semibold text-background" : "border border-line text-muted hover:text-foreground"}`}
                key={item.id}
                onClick={() => setTab(item.id)}
                role="tab"
                type="button"
              >
                <CoachTabIcon name={item.icon} />
                {item.label}
              </button>
            );
          })}
        </div>
      </div>

      <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-line bg-background px-4 py-3">
        <div>
          <p className="text-sm font-semibold">Period</p>
          <p className="text-xs text-muted">Filters Performance, targets, commission and related metrics.</p>
        </div>
        <label className="grid gap-1 text-sm">
          <span className="sr-only">Coach period</span>
          <input aria-label="Coach period" className="field max-w-[11rem]" onChange={(event) => setMonth(event.target.value)} type="month" value={month} />
        </label>
      </div>

      {coach.isLoading ? <p className="mt-4 text-sm text-muted" role="status">Loading coach details.</p> : null}
      {coach.error instanceof ApiError ? <p className="mt-4" role="alert">{coach.error.message}</p> : null}
      {data && tab === "Overview" ? <CoachOverview coach={data} /> : null}
      {data && tab === "Assigned Members" ? <Members coach={data} onChanged={refresh} /> : null}
      {data && tab === "Performance" ? (
        <CoachPerformance
          activePtClients={data.activePtClients}
          coachId={data.id}
          month={month}
          newPtClients={data.newPtClients}
          ptPackagesSold={data.ptPackagesSold}
          ptSessionsCompleted={data.ptSessionsCompleted}
        />
      ) : null}
      {data && tab === "Target and Coach Credit" ? (
        <CoachCredits coachId={data.id} onTargetsChanged={refresh} range={range} targets={data.targets} />
      ) : null}
      {data && tab === "Commission & Incentives" ? <Compensation coach={data} range={range} onChanged={refresh} /> : null}
      {data && tab === "History" ? <CoachHistory items={data.history ?? []} subject={data.name} /> : null}
    </FormDialog>
  );
}

function Members({ coach, onChanged }: { coach: Coach; onChanged: () => void }) {
  const members = useQuery({ queryKey: ["trainer-member-options"], queryFn: () => api<Page<GymMember>>("/members?page=1&pageSize=100&sort=name") });
  const trainers = useQuery({ queryKey: ["trainer-workspace"], queryFn: () => api<TrainerRow[]>("/trainers/workspace") });
  const [memberId, setMemberId] = useState("");
  const [trainerId, setTrainerId] = useState(coach.id);
  const today = new Date().toISOString().slice(0, 10);
  const assign = useMutation({ mutationFn: () => api(`/trainers/${coach.id}/members`, { method: "POST", body: JSON.stringify({ memberId, startDate: today }) }), onSuccess: onChanged });
  const change = useMutation({ mutationFn: (id: string) => api(`/trainers/assignments/${id}/change`, { method: "POST", body: JSON.stringify({ trainerId, startDate: today }) }), onSuccess: onChanged });
  const end = useMutation({ mutationFn: (id: string) => api(`/trainers/assignments/${id}/end`, { method: "POST", body: JSON.stringify({ endDate: today }) }), onSuccess: onChanged });
  return (
    <div className="mt-4 grid gap-3 text-sm">
      <div className="flex flex-wrap gap-2">
        <select aria-label="Member" className="field max-w-xs" onChange={(event) => setMemberId(event.target.value)} value={memberId}>
          <option value="">Assign member</option>
          {(members.data?.items ?? []).map((member) => <option key={member.id} value={member.id}>{member.fullName}</option>)}
        </select>
        <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" disabled={!memberId || assign.isPending} onClick={() => assign.mutate()} type="button">Assign member</button>
      </div>
      {coach.assignments.length === 0 ? <p className="text-muted">No members are assigned to this coach.</p> : null}
      <ScrollableGrid>
      <ul className="grid w-max min-w-full gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {coach.assignments.map((item) => (
          <li className="grid min-w-64 content-start gap-2 rounded-2xl border border-line bg-background p-4" key={item.id}>
            <p className="text-base font-semibold">{item.memberName}</p>
            <p className="text-muted">{item.phone || "No phone"}</p>
            <p>{item.status}</p>
            <p className="text-muted">{item.startDate}{item.endDate ? ` to ${item.endDate}` : ""}</p>
            <p>{item.membershipStatus || "No membership"}{item.membershipEnd ? ` until ${item.membershipEnd}` : ""}</p>
            <Link className="inline-flex min-h-11 items-center font-semibold text-accent" href={memberDetailsHref(item.memberId)}>View member</Link>
            {item.status === "Active" ? (
              <div className="grid gap-2">
                <select aria-label={`Change coach for ${item.memberName}`} className="field" onChange={(event) => setTrainerId(event.target.value)} value={trainerId}>
                  {(trainers.data ?? []).map((trainer) => <option key={trainer.id} value={trainer.id}>{trainer.name}</option>)}
                </select>
                <div className="flex flex-wrap gap-2">
                  <button className="min-h-11 rounded-full border border-line px-3" onClick={() => change.mutate(item.id)} type="button">Change coach</button>
                  <button className="min-h-11 rounded-full border border-line px-3" onClick={() => end.mutate(item.id)} type="button">End assignment</button>
                </div>
              </div>
            ) : null}
          </li>
        ))}
      </ul>
      </ScrollableGrid>
    </div>
  );
}

function Compensation({ coach, range, onChanged }: { coach: Coach; range: { from: string; to: string }; onChanged: () => void }) {
  const [name, setName] = useState("PT revenue commission");
  const [percent, setPercent] = useState("5");
  const [reward, setReward] = useState("500");
  const commission = useMutation({ mutationFn: () => api(`/trainers/${coach.id}/commission-rules`, { method: "POST", body: JSON.stringify({ name, kind: "PercentOfEligiblePtRevenue", percent: Number(percent), fixedAmount: null, start: range.from, end: range.to, requiresApproval: true }) }), onSuccess: onChanged });
  const incentive = useMutation({ mutationFn: () => api(`/trainers/${coach.id}/performance-incentives`, { method: "POST", body: JSON.stringify({ name: "New client incentive", metric: "NewPtClients", threshold: 1, rewardKind: "FixedOnAchievement", fixedAmount: Number(reward), percent: null, start: range.from, end: range.to, requiresApproval: true }) }), onSuccess: onChanged });
  const calculate = useMutation({ mutationFn: () => api(`/trainers/${coach.id}/compensation/calculate?from=${range.from}&to=${range.to}`, { method: "POST" }), onSuccess: onChanged });
  const decide = useMutation({ mutationFn: ({ id, status }: { id: string; status: string }) => api(`/trainers/compensation/${id}/decide`, { method: "POST", body: JSON.stringify({ status }) }), onSuccess: onChanged });
  return (
    <div className="mt-4 grid gap-4">
      <section className="rounded-2xl border border-line bg-background p-4">
        <h3 className="text-base font-semibold">Commission rule</h3>
        <p className="mt-1 max-w-3xl text-sm leading-6 text-muted">A percentage of eligible personal-training revenue for the selected period. Revenue is not recorded yet, so the rule is saved and the amount stays uncalculated until that revenue exists.</p>
        <div className="mt-4 grid gap-3 sm:grid-cols-[minmax(0,1fr)_7rem_auto] sm:items-end">
          <label className="grid gap-1 text-sm">Rule name
            <input aria-label="Commission name" className="field w-full" onChange={(event) => setName(event.target.value)} value={name} />
          </label>
          <label className="grid gap-1 text-sm">Percent
            <input aria-label="Commission percent" className="field w-full" inputMode="decimal" onChange={(event) => setPercent(event.target.value)} value={percent} />
          </label>
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={commission.isPending} onClick={() => commission.mutate()} type="button">Save commission rule</button>
        </div>
        <RuleList empty="No commission rules for this period." rules={coach.commissionRules} />
      </section>
      <section className="rounded-2xl border border-line bg-background p-4">
        <h3 className="text-base font-semibold">Performance incentive</h3>
        <p className="mt-1 max-w-3xl text-sm leading-6 text-muted">A fixed reward when this coach gains at least one new personal-training client in the selected period. It is calculated on its own and is not marked paid.</p>
        <div className="mt-4 grid gap-3 sm:grid-cols-[minmax(0,12rem)_auto] sm:items-end">
          <label className="grid gap-1 text-sm">Fixed reward
            <input aria-label="Incentive amount" className="field w-full" inputMode="decimal" onChange={(event) => setReward(event.target.value)} value={reward} />
          </label>
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={incentive.isPending} onClick={() => incentive.mutate()} type="button">Save new-client incentive</button>
        </div>
        <RuleList empty="No incentives for this period." rules={coach.incentiveRules} />
        <div className="mt-4 flex flex-col gap-3 border-t border-line pt-4 sm:flex-row sm:items-center sm:justify-between">
          <p className="max-w-2xl text-sm leading-6 text-muted">Calculate this period to turn the active rules into amounts. Each result starts as Calculated, then moves to Pending approval, and can be approved or rejected.</p>
          <button className="min-h-11 shrink-0 rounded-full border border-accent px-4 font-semibold text-accent disabled:opacity-60" disabled={calculate.isPending} onClick={() => calculate.mutate()} type="button">Calculate period</button>
        </div>
      </section>
      <ScrollableGrid>
      <ul className="grid w-max min-w-full gap-2">
        {coach.calculations.length === 0 ? <li className="rounded-2xl border border-dashed border-line p-4 text-sm text-muted">No calculations for this period yet.</li> : null}
        {coach.calculations.map((item) => (
          <li className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-line p-4 text-sm" key={item.id}>
            <div>
              <p className="font-semibold">{item.ruleName}</p>
              <p className="text-muted">{item.kind} · {item.amount} · {item.status}</p>
              <p className="text-muted">{item.basis}</p>
            </div>
            <div className="flex flex-wrap gap-2">
              {item.status === "Calculated" ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => decide.mutate({ id: item.id, status: "PendingApproval" })} type="button">Submit</button> : null}
              {item.status === "PendingApproval" ? <button className="min-h-11 rounded-full bg-accent px-3 font-semibold text-background" onClick={() => decide.mutate({ id: item.id, status: "Approved" })} type="button">Approve</button> : null}
              {item.status === "PendingApproval" ? <button className="min-h-11 rounded-full border border-line px-3" onClick={() => decide.mutate({ id: item.id, status: "Rejected" })} type="button">Reject</button> : null}
            </div>
          </li>
        ))}
      </ul>
      </ScrollableGrid>
    </div>
  );
}

function RuleList({ empty, rules }: { empty: string; rules: Rule[] }) {
  if (rules.length === 0) {
    return <p className="mt-4 text-sm text-muted">{empty}</p>;
  }

  return (
    <ScrollableGrid className="mt-4">
    <ul className="grid w-max min-w-full gap-2">
      {rules.map((rule) => (
        <li className="rounded-xl border border-line px-3 py-2 text-sm" key={rule.id}>
          <span className="font-semibold">{rule.name}</span>
          <span className="text-muted"> · {rule.detail}{rule.requiresApproval ? " · Approval required" : ""}</span>
        </li>
      ))}
    </ul>
    </ScrollableGrid>
  );
}
