"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { ApiError } from "../../../lib/api";
import {
  approvePayroll,
  bulkPay,
  draftPayroll,
  getPayrollRun,
  listCompensation,
  listPayrollRuns,
  money,
  periodLabel,
  preparePayroll,
  stepFromStatus,
  submitPayroll,
  type PayrollRunDetails,
  type PayrollStep,
} from "../../../lib/payroll";
import { getSession, subscribeSession } from "../../../lib/session";
import { ActionBar, CompensationPanel, DetailPanel, issueText, SummaryCards } from "./PayrollPanels";
import { PayrollSteps } from "./PayrollSteps";
import { PayrollTable } from "./PayrollTable";

const PAGE_SIZE = 6;

export default function PayrollPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [runId, setRunId] = useState<string | null>(null);
  const [step, setStep] = useState<PayrollStep>("prepare");
  const [selected, setSelected] = useState<string | null>(null);
  const [term, setTerm] = useState("");
  const [filter, setFilter] = useState<"all" | "issues" | "ready">("all");
  const [page, setPage] = useState(1);

  const runs = useQuery({ queryKey: ["payroll-runs", tenantId], enabled: tenantId !== null, queryFn: listPayrollRuns });
  const run = useQuery({
    queryKey: ["payroll-run", tenantId, runId],
    enabled: tenantId !== null && runId !== null,
    queryFn: () => getPayrollRun(runId!),
  });
  const compensation = useQuery({ queryKey: ["payroll-compensation", tenantId], enabled: tenantId !== null, queryFn: listCompensation });

  const prepare = useMutation({
    mutationFn: () => preparePayroll(year, month),
    onSuccess: async (data) => {
      setRunId(data.id);
      setStep(stepFromStatus(data.status));
      setPage(1);
      await client.invalidateQueries({ queryKey: ["payroll-runs", tenantId] });
      await client.invalidateQueries({ queryKey: ["payroll-run", tenantId, data.id] });
    },
  });
  const submit = useMutation({ mutationFn: () => submitPayroll(runId!), onSuccess: (data) => { setStep("review"); void refresh(data); } });
  const toDraft = useMutation({ mutationFn: () => draftPayroll(runId!), onSuccess: (data) => { setStep("prepare"); void refresh(data); } });
  const approve = useMutation({ mutationFn: () => approvePayroll(runId!), onSuccess: (data) => { setStep("pay"); void refresh(data); } });
  const payBulk = useMutation({
    mutationFn: () => bulkPay(runId!, new Date().toISOString().slice(0, 10), "BankTransfer", `PAY-${year}${String(month).padStart(2, "0")}`),
    onSuccess: (data) => void refresh(data),
  });

  async function refresh(data: PayrollRunDetails) {
    setRunId(data.id);
    await client.invalidateQueries({ queryKey: ["payroll-run", tenantId, data.id] });
    await client.invalidateQueries({ queryKey: ["payroll-runs", tenantId] });
  }

  const details = run.data;
  const filtered = useMemo(() => {
    const items = details?.lines ?? [];
    const needle = term.trim().toLowerCase();
    return items.filter((line) => {
      const match = !needle || `${line.employeeName} ${line.employeeCode} ${line.staffType}`.toLowerCase().includes(needle);
      const issue = line.missingSalary || line.unapprovedCommission || line.unconfirmedAttendance;
      if (filter === "issues") return match && issue;
      if (filter === "ready") return match && !issue;
      return match;
    });
  }, [details, term, filter]);
  const pageLines = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const selectedLine = filtered.find((line) => line.id === selected) ?? pageLines[0] ?? null;
  const issues = (details?.lines ?? []).filter((line) => line.missingSalary || line.unapprovedCommission || line.unconfirmedAttendance);

  return (
    <PageState error={runs.error instanceof ApiError ? runs.error.message : null} loading={runs.isLoading} onRetry={() => void runs.refetch()}>
      <div className="grid gap-5">
        <header className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="text-sm text-muted">Staff & Payroll</p>
            <h1 className="text-3xl font-semibold">Payroll</h1>
            <p className="mt-1 text-sm text-muted">Prepare, review, approve, and record staff pay for a monthly period.</p>
          </div>
          <div className="flex flex-wrap items-end gap-2">
            <label className="grid gap-1 text-sm">Year<input aria-label="Year" className="field w-24" max={2100} min={2000} onChange={(event) => setYear(Number(event.target.value))} type="number" value={year} /></label>
            <label className="grid gap-1 text-sm">Month<input aria-label="Month" className="field w-20" max={12} min={1} onChange={(event) => setMonth(Number(event.target.value))} type="number" value={month} /></label>
            <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={prepare.isPending} onClick={() => prepare.mutate()} type="button">{prepare.isPending ? "Loading..." : "Load period"}</button>
          </div>
        </header>

        <PayrollSteps current={step} onSelect={setStep} status={details?.status} />
        {prepare.error instanceof ApiError ? <p role="alert">{prepare.error.message}</p> : null}
        {submit.error instanceof ApiError ? <p role="alert">{submit.error.message}</p> : null}
        {approve.error instanceof ApiError ? <p role="alert">{approve.error.message}</p> : null}

        {!details ? (
          <section className="rounded-2xl border border-line bg-surface p-6">
            <h2 className="text-lg font-semibold">Recent payroll runs</h2>
            {(runs.data ?? []).length === 0 ? <p className="mt-3 text-muted">No payroll runs yet. Choose a month and load the period.</p> : (
              <ul className="mt-4 grid gap-2">
                {(runs.data ?? []).map((item) => (
                  <li key={item.id}>
                    <button className="flex w-full items-center justify-between rounded-xl border border-line px-4 py-3 text-left hover:border-accent" onClick={() => { setRunId(item.id); setYear(item.periodYear); setMonth(item.periodMonth); setStep(stepFromStatus(item.status)); setPage(1); }} type="button">
                      <span>{periodLabel(item.periodYear, item.periodMonth)} · {item.status}</span>
                      <span className="text-sm text-muted">{item.staffCount} staff · {money(item.netPayable, item.currency)}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
            <CompensationPanel rows={compensation.data ?? []} onSaved={() => void client.invalidateQueries({ queryKey: ["payroll-compensation", tenantId] })} />
          </section>
        ) : (
          <div className="grid gap-4 xl:grid-cols-[minmax(0,1.7fr)_minmax(18rem,1fr)]">
            <section className="grid gap-4">
              <SummaryCards details={details} onChangePeriod={() => setRunId(null)} />
              <div className="rounded-2xl border border-line bg-surface p-4 shadow-sm">
                <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                  <h2 className="text-lg font-semibold">Staff salary review</h2>
                  <div className="flex flex-wrap gap-2">
                    <input aria-label="Search staff" className="field max-w-xs" onChange={(event) => { setTerm(event.target.value); setPage(1); }} placeholder="Search staff" value={term} />
                    <select aria-label="Filter staff" className="field w-auto" onChange={(event) => { setFilter(event.target.value as typeof filter); setPage(1); }} value={filter}>
                      <option value="all">All staff</option>
                      <option value="issues">Needs review</option>
                      <option value="ready">Ready</option>
                    </select>
                  </div>
                </div>
                {run.isLoading ? <p role="status">Loading payroll lines.</p> : (
                  <PayrollTable currency={details.currency} lines={pageLines} onPage={setPage} onSelect={setSelected} page={page} pageSize={PAGE_SIZE} selectedId={selectedLine?.id ?? null} total={filtered.length} />
                )}
              </div>
              {issues.length > 0 ? (
                <section className="rounded-2xl border border-amber-500/30 bg-surface p-4">
                  <h3 className="font-semibold">Staff requiring attention</h3>
                  <ul className="mt-3 grid gap-2 text-sm">
                    {issues.map((line) => (
                      <li className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-line px-3 py-2" key={line.id}>
                        <span>{line.employeeName}: {issueText(line)}</span>
                        <button className="rounded-full border border-line px-3 py-1" onClick={() => setSelected(line.id)} type="button">Resolve</button>
                      </li>
                    ))}
                  </ul>
                </section>
              ) : null}
              <ActionBar details={details} onApprove={() => approve.mutate()} onBulk={() => payBulk.mutate()} onDraft={() => toDraft.mutate()} onSubmit={() => submit.mutate()} pending={submit.isPending || approve.isPending || payBulk.isPending || toDraft.isPending} step={step} />
            </section>
            <DetailPanel canEdit={details.canEdit} canPay={details.canPay} currency={details.currency} details={details} line={selectedLine} onChanged={async () => { await client.invalidateQueries({ queryKey: ["payroll-run", tenantId, details.id] }); }} />
          </div>
        )}
      </div>
    </PageState>
  );
}
