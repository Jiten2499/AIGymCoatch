"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, useSyncExternalStore } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { ExpenseRow, ExpenseSummary } from "../../../lib/gym";
import type { Page } from "../../../lib/types";
import { getSession, subscribeSession } from "../../../lib/session";
import { CategoryDialog, ExpenseDialog } from "./ExpenseDialogs";

export default function ExpensesPage() {
  const client = useQueryClient();
  const session = useSyncExternalStore(subscribeSession, getSession, () => null);
  const tenantId = session?.currentTenant?.id ?? null;
  const [term, setTerm] = useState("");
  const [adding, setAdding] = useState(false);
  const [category, setCategory] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const expenses = useQuery({
    queryKey: ["expenses", tenantId, term],
    enabled: tenantId !== null,
    queryFn: () => api<Page<ExpenseRow>>(`/expenses?page=1&pageSize=50&sort=date${term.trim() ? `&term=${encodeURIComponent(term.trim())}` : ""}`),
  });
  const summary = useQuery({
    queryKey: ["expense-summary", tenantId],
    enabled: tenantId !== null,
    queryFn: () => api<ExpenseSummary>("/expenses/summary"),
  });
  const voidExpense = useMutation({
    mutationFn: () => api(`/expenses/${selected}/void`, { method: "POST" }),
    onSuccess: async () => {
      setSelected(null);
      await client.invalidateQueries({ queryKey: ["expenses", tenantId] });
      await client.invalidateQueries({ queryKey: ["expense-summary", tenantId] });
      await client.invalidateQueries({ queryKey: ["ledger-summary", tenantId] });
      await client.invalidateQueries({ queryKey: ["ledger-transactions", tenantId] });
      await client.invalidateQueries({ queryKey: ["financial-accounts", tenantId] });
    },
  });

  return (
    <PageState error={expenses.error instanceof ApiError ? expenses.error.message : null} loading={expenses.isLoading} onRetry={() => void expenses.refetch()}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold">Expenses</h1>
          <p className="mt-2 text-muted">
            {summary.data ? `Today ${summary.data.todayAmount} ${summary.data.currency} · This month ${summary.data.monthAmount} ${summary.data.currency} · ${summary.data.totalCount} recorded` : "Operating costs recorded for this gym."}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setAdding(true)} type="button">Add expense</button>
          <button className="min-h-11 rounded-full border border-line px-4" onClick={() => setCategory(true)} type="button">Add category</button>
        </div>
      </div>
      {summary.data && summary.data.categories.length > 0 ? (
        <ul className="mt-6 flex flex-wrap gap-3">
          {summary.data.categories.map((item) => <li className="rounded-2xl border border-line px-4 py-3" key={item.categoryId}>{item.name} {item.amount}</li>)}
        </ul>
      ) : null}
      <input aria-label="Search expenses" className="field mt-6 max-w-sm" onChange={(event) => setTerm(event.target.value)} placeholder="Search description" value={term} />
      {voidExpense.error instanceof ApiError ? <p className="mt-3" role="alert">{voidExpense.error.message}</p> : null}
      <div className="mt-4">
        <DataTable
          columns={[
            { header: "Date", cell: (row) => row.expenseDate },
            { header: "Category", cell: (row) => row.category },
            { header: "Description", cell: (row) => row.description },
            { header: "Amount", cell: (row) => `${row.amount} ${row.currency}` },
            { header: "Method", cell: (row) => row.paymentMethod },
            { header: "Recorded by", cell: (row) => row.recordedBy || "—" },
            { header: "Status", cell: (row) => row.status },
          ]}
          empty="No expenses were returned."
          rowKey={(row) => row.id}
          rows={expenses.data?.items ?? []}
          selection={{ isSelected: (row) => selected === row.id, label: (row) => `Select ${row.description}`, onToggle: (row) => setSelected((current) => (current === row.id ? null : row.id)) }}
        />
      </div>
      {selected ? <button className="mt-4 min-h-11 rounded-full border border-line px-4" onClick={() => voidExpense.mutate()} type="button">Void expense</button> : null}
      {adding ? <ExpenseDialog onCancel={() => setAdding(false)} onCreated={async () => { setAdding(false); await client.invalidateQueries({ queryKey: ["expenses", tenantId] }); await client.invalidateQueries({ queryKey: ["expense-summary", tenantId] }); await client.invalidateQueries({ queryKey: ["ledger-summary", tenantId] }); await client.invalidateQueries({ queryKey: ["ledger-transactions", tenantId] }); await client.invalidateQueries({ queryKey: ["financial-accounts", tenantId] }); }} /> : null}
      {category ? <CategoryDialog onCancel={() => setCategory(false)} onCreated={() => setCategory(false)} /> : null}
    </PageState>
  );
}
