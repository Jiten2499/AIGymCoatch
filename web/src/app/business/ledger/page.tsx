"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { ScrollableGrid } from "../../../components/ScrollableGrid";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import type { FinancialAccount, LedgerSummary, LedgerTransaction } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { getSession, subscribeSession } from "../../../lib/session";

type RangeKey = "today" | "week" | "month" | "custom";

function startOfWeek(date: Date) {
  const day = date.getDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  const start = new Date(date);
  start.setDate(date.getDate() + mondayOffset);
  return start;
}

function iso(date: Date) {
  return date.toISOString().slice(0, 10);
}

export default function LedgerPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const today = useMemo(() => new Date(), []);
  const [range, setRange] = useState<RangeKey>("month");
  const [from, setFrom] = useState(iso(new Date(today.getFullYear(), today.getMonth(), 1)));
  const [to, setTo] = useState(iso(today));
  const [entryType, setEntryType] = useState("");
  const [accountId, setAccountId] = useState("");
  const [category, setCategory] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [transferring, setTransferring] = useState(false);
  const [managing, setManaging] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);

  const bounds = useMemo(() => {
    if (range === "today") return { from: iso(today), to: iso(today) };
    if (range === "week") return { from: iso(startOfWeek(today)), to: iso(today) };
    if (range === "month") return { from: iso(new Date(today.getFullYear(), today.getMonth(), 1)), to: iso(today) };
    return { from, to };
  }, [from, range, to, today]);

  const features = useQuery({
    queryKey: ["features", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<{ featureCode: string; enabled: boolean }[]>("/features"),
  });
  const ledgerEnabled = features.data?.some((feature) => feature.featureCode === "GYM_FINANCIAL_LEDGER" && feature.enabled) === true;

  const summary = useQuery({
    queryKey: ["ledger-summary", tenantId, bounds.from, bounds.to],
    enabled: tenantId !== null && ledgerEnabled,
    queryFn: () => api<LedgerSummary>(`/ledger/summary?from=${bounds.from}&to=${bounds.to}`),
  });
  const accounts = useQuery({
    queryKey: ["financial-accounts", tenantId],
    enabled: tenantId !== null && ledgerEnabled,
    queryFn: () => api<FinancialAccount[]>("/financial-accounts"),
  });
  const params = new URLSearchParams({
    from: bounds.from,
    to: bounds.to,
    page: String(page),
    pageSize: "20",
  });
  if (entryType) params.set("entryType", entryType);
  if (accountId) params.set("financialAccountId", accountId);
  if (category.trim()) params.set("category", category.trim());
  if (status) params.set("status", status);
  const transactions = useQuery({
    queryKey: ["ledger-transactions", tenantId, params.toString()],
    enabled: tenantId !== null && ledgerEnabled,
    queryFn: () => api<Page<LedgerTransaction>>(`/ledger/transactions?${params}`),
  });

  const voidEntry = useMutation({
    mutationFn: () => api(`/ledger/transactions/${selected}/void`, { method: "POST" }),
    onSuccess: async () => {
      setSelected(null);
      await client.invalidateQueries({ queryKey: ["ledger-summary", tenantId] });
      await client.invalidateQueries({ queryKey: ["ledger-transactions", tenantId] });
      await client.invalidateQueries({ queryKey: ["financial-accounts", tenantId] });
    },
  });

  if (features.isLoading) {
    return <PageState error={null} loading onRetry={() => void features.refetch()}><span /></PageState>;
  }
  if (!ledgerEnabled) {
    return (
      <PageState error={null} loading={false} onRetry={() => undefined}>
        <h1 className="text-3xl font-semibold">Ledger</h1>
        <p className="mt-2 text-muted">Financial ledger is not enabled for this workspace.</p>
      </PageState>
    );
  }

  const error = summary.error ?? transactions.error ?? accounts.error ?? voidEntry.error;
  const totalBalance = (accounts.data ?? []).filter((account) => account.isActive).reduce((sum, account) => sum + account.balance, 0);

  return (
    <PageState error={error instanceof ApiError ? error.message : null} loading={summary.isLoading || transactions.isLoading} onRetry={() => { void summary.refetch(); void transactions.refetch(); }}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Ledger</h1>
          <p className="mt-2 text-muted">
            {summary.data
              ? `Money in ${summary.data.moneyIn} · Money out ${summary.data.moneyOut} · Net ${summary.data.netCashFlow} ${summary.data.currency}`
              : "Cash movement posted from payments, expenses, and transfers."}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link className="inline-flex min-h-11 items-center rounded-full bg-accent px-4 font-semibold text-background" href="/business/memberships">Receive money</Link>
          <Link className="inline-flex min-h-11 items-center rounded-full border border-line px-4" href="/business/expenses">Add expense</Link>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setTransferring(true)} type="button">Transfer money</button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setManaging(true)} type="button">Accounts</button>
        </div>
      </div>

      <div className="mt-6 grid gap-3 sm:grid-cols-4">
        <Metric label="Money in" value={summary.data?.moneyIn} currency={summary.data?.currency} />
        <Metric label="Money out" value={summary.data?.moneyOut} currency={summary.data?.currency} />
        <Metric label="Net cash flow" value={summary.data?.netCashFlow} currency={summary.data?.currency} />
        <Metric label="Account balance" value={totalBalance} currency={summary.data?.currency ?? accounts.data?.[0]?.currency} />
      </div>

      <div className="mt-6 flex flex-wrap gap-2">
        {(["today", "week", "month", "custom"] as const).map((key) => (
          <button className={`min-h-11 rounded-full border px-4 ${range === key ? "border-accent text-accent" : "border-line"}`} key={key} onClick={() => { setRange(key); setPage(1); }} type="button">
            {key === "today" ? "Today" : key === "week" ? "This week" : key === "month" ? "This month" : "Custom"}
          </button>
        ))}
        {range === "custom" ? (
          <>
            <input aria-label="From date" className="field max-w-[10rem]" onChange={(event) => setFrom(event.target.value)} type="date" value={from} />
            <input aria-label="To date" className="field max-w-[10rem]" onChange={(event) => setTo(event.target.value)} type="date" value={to} />
          </>
        ) : null}
      </div>

      <div className="mt-4 flex flex-wrap gap-2">
        <select aria-label="Type" className="field max-w-[10rem]" onChange={(event) => { setEntryType(event.target.value); setPage(1); }} value={entryType}>
          <option value="">All types</option>
          <option>MoneyIn</option>
          <option>MoneyOut</option>
          <option>Transfer</option>
        </select>
        <select aria-label="Account" className="field max-w-[12rem]" onChange={(event) => { setAccountId(event.target.value); setPage(1); }} value={accountId}>
          <option value="">All accounts</option>
          {(accounts.data ?? []).map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
        </select>
        <input aria-label="Category" className="field max-w-[12rem]" onChange={(event) => { setCategory(event.target.value); setPage(1); }} placeholder="Category" value={category} />
        <select aria-label="Status" className="field max-w-[10rem]" onChange={(event) => { setStatus(event.target.value); setPage(1); }} value={status}>
          <option value="">All statuses</option>
          <option>Posted</option>
          <option>Voided</option>
          <option>Draft</option>
        </select>
      </div>

      {(summary.data?.accounts.length ?? 0) > 0 ? (
        <ul className="mt-6 flex flex-wrap gap-3">
          {summary.data!.accounts.map((account) => (
            <li className="rounded-2xl border border-line px-4 py-3" key={account.accountId}>
              {account.accountName}: in {account.moneyIn} / out {account.moneyOut} / bal {account.balance}
            </li>
          ))}
        </ul>
      ) : null}

      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Date", cell: (row) => row.entryDate },
            { header: "Description", cell: (row) => row.description },
            { header: "Type", cell: (row) => row.entryType },
            { header: "Account", cell: (row) => row.accountName || "—" },
            { header: "Category", cell: (row) => row.category || "—" },
            { header: "Amount", cell: (row) => `${row.amount} ${row.currency}` },
            { header: "Status", cell: (row) => row.status },
            { header: "Recorded by", cell: (row) => row.recordedBy || "—" },
          ]}
          empty="No ledger transactions in this range."
          rowKey={(row) => row.id}
          rows={transactions.data?.items ?? []}
          selection={{ isSelected: (row) => selected === row.id, label: (row) => `Select ${row.description}`, onToggle: (row) => setSelected((current) => (current === row.id ? null : row.id)) }}
        />
      </div>
      <div className="mt-4 flex flex-wrap gap-2">
        {selected ? <button className="min-h-11 rounded-full border border-line px-4" onClick={() => voidEntry.mutate()} type="button">Void transaction</button> : null}
        {(transactions.data?.totalCount ?? 0) > page * 20 ? <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setPage((current) => current + 1)} type="button">Next page</button> : null}
        {page > 1 ? <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setPage((current) => current - 1)} type="button">Previous page</button> : null}
      </div>

      {transferring ? (
        <TransferDialog
          accounts={(accounts.data ?? []).filter((account) => account.isActive)}
          onCancel={() => setTransferring(false)}
          onCreated={async () => {
            setTransferring(false);
            await client.invalidateQueries({ queryKey: ["ledger-summary", tenantId] });
            await client.invalidateQueries({ queryKey: ["ledger-transactions", tenantId] });
            await client.invalidateQueries({ queryKey: ["financial-accounts", tenantId] });
          }}
        />
      ) : null}
      {managing ? (
        <AccountsDialog
          accounts={accounts.data ?? []}
          onClose={() => setManaging(false)}
          onChanged={async () => {
            await client.invalidateQueries({ queryKey: ["financial-accounts", tenantId] });
            await client.invalidateQueries({ queryKey: ["ledger-summary", tenantId] });
          }}
        />
      ) : null}
    </PageState>
  );
}

function Metric({ label, value, currency }: { label: string; value?: number; currency?: string }) {
  return (
    <div className="rounded-2xl border border-line px-4 py-3">
      <p className="text-sm text-muted">{label}</p>
      <p className="mt-1 text-xl font-semibold">{value ?? "—"}{currency ? ` ${currency}` : ""}</p>
    </div>
  );
}

function TransferDialog({ accounts, onCancel, onCreated }: { accounts: FinancialAccount[]; onCancel: () => void; onCreated: () => Promise<void> }) {
  const [fromAccountId, setFromAccountId] = useState(accounts[0]?.id ?? "");
  const [toAccountId, setToAccountId] = useState(accounts[1]?.id ?? accounts[0]?.id ?? "");
  const [amount, setAmount] = useState("");
  const [transferDate, setTransferDate] = useState(iso(new Date()));
  const [notes, setNotes] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => api("/ledger/transfers", {
      method: "POST",
      body: JSON.stringify({ fromAccountId, toAccountId, amount: Number(amount), transferDate, notes: notes.trim() || null }),
    }),
    onSuccess: () => void onCreated(),
    onError: (error) => setMessage(error instanceof ApiError ? error.message : "Transfer failed."),
  });

  return (
    <FormDialog onCancel={onCancel} title="Transfer money">
      <form className="mt-4 grid gap-3" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
        <FormField label="From account">
          <select className="field" onChange={(event) => setFromAccountId(event.target.value)} value={fromAccountId}>
            {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
          </select>
        </FormField>
        <FormField label="To account">
          <select className="field" onChange={(event) => setToAccountId(event.target.value)} value={toAccountId}>
            {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
          </select>
        </FormField>
        <FormField label="Amount"><input className="field" min={0} onChange={(event) => setAmount(event.target.value)} step="0.01" type="number" value={amount} /></FormField>
        <FormField label="Date"><input className="field" onChange={(event) => setTransferDate(event.target.value)} type="date" value={transferDate} /></FormField>
        <FormField label="Notes"><textarea className="field min-h-20 py-2" onChange={(event) => setNotes(event.target.value)} value={notes} /></FormField>
        {message ? <p role="alert">{message}</p> : null}
        <FormActions cancel={onCancel} pending={save.isPending} submitLabel="Transfer" />
      </form>
    </FormDialog>
  );
}

function AccountsDialog({ accounts, onClose, onChanged }: { accounts: FinancialAccount[]; onClose: () => void; onChanged: () => Promise<void> }) {
  const [name, setName] = useState("");
  const [kind, setKind] = useState("Cash");
  const [message, setMessage] = useState<string | null>(null);
  const create = useMutation({
    mutationFn: () => api("/financial-accounts", { method: "POST", body: JSON.stringify({ name, accountKind: kind }) }),
    onSuccess: async () => { setName(""); await onChanged(); },
    onError: (error) => setMessage(error instanceof ApiError ? error.message : "Could not create account."),
  });
  const toggle = useMutation({
    mutationFn: (account: FinancialAccount) => api(`/financial-accounts/${account.id}/status`, { method: "POST", body: JSON.stringify({ active: !account.isActive }) }),
    onSuccess: () => void onChanged(),
    onError: (error) => setMessage(error instanceof ApiError ? error.message : "Could not update account."),
  });

  return (
    <FormDialog onCancel={onClose} title="Financial accounts">
      <form className="mt-4 grid gap-3" onSubmit={(event) => { event.preventDefault(); create.mutate(); }}>
        <ScrollableGrid>
        <ul className="grid w-max min-w-full gap-2">
          {accounts.map((account) => (
            <li className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-line px-3 py-2" key={account.id}>
              <span>{account.name} · {account.accountKind} · {account.balance} {account.currency} · {account.isActive ? "Active" : "Inactive"}</span>
              <button className="min-h-11 rounded-full border border-line px-3" onClick={() => toggle.mutate(account)} type="button">{account.isActive ? "Deactivate" : "Activate"}</button>
            </li>
          ))}
        </ul>
        </ScrollableGrid>
        <FormField label="Name"><input className="field" onChange={(event) => setName(event.target.value)} value={name} /></FormField>
        <FormField label="Kind">
          <select className="field" onChange={(event) => setKind(event.target.value)} value={kind}>
            {["Cash", "Bank", "Upi", "Card", "Other"].map((value) => <option key={value}>{value}</option>)}
          </select>
        </FormField>
        {message ? <p role="alert">{message}</p> : null}
        <FormActions cancel={onClose} pending={create.isPending} submitLabel="Create account" />
      </form>
    </FormDialog>
  );
}
