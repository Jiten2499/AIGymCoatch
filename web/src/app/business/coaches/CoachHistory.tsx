"use client";

import { useMemo, useState } from "react";
import { DataTable } from "../../../components/DataTable";
import { FormDialog } from "../../../components/FormDialog";

export type CoachHistoryItem = {
  id: string;
  at: string;
  category: string;
  action: string;
  activity: string;
  details: string;
  subject: string;
  performedBy: string | null;
  oldValues: string | null;
  newValues: string | null;
};

type HistoryInput = Partial<CoachHistoryItem> & {
  detail?: string;
  Detail?: string;
  Details?: string;
  Activity?: string;
  Category?: string;
  Action?: string;
  At?: string;
  Id?: string;
  Subject?: string;
  PerformedBy?: string | null;
  OldValues?: string | null;
  NewValues?: string | null;
};

const categories = ["All", "Profile", "Assignment", "Session", "Target", "Credit", "Commission", "Employment"] as const;

function formatWhen(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function dayKey(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return date.toISOString().slice(0, 10);
}

function humanizeAction(action: string) {
  if (!action) {
    return "Activity recorded";
  }
  return action
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/^./, (letter) => letter.toUpperCase());
}

function inferCategory(action: string) {
  const value = action.toLowerCase();
  if (value.includes("assign") || value.includes("member")) return "Assignment";
  if (value.includes("session")) return "Session";
  if (value.includes("target")) return "Target";
  if (value.includes("credit") || value.includes("reward")) return "Credit";
  if (value.includes("commission") || value.includes("incentive") || value.includes("compensation")) return "Commission";
  if (value.includes("status") || value.includes("employment")) return "Employment";
  return "Profile";
}

export function normalizeCoachHistory(items: HistoryInput[], subjectFallback: string): CoachHistoryItem[] {
  return items.map((item, index) => {
    const action = item.action ?? item.Action ?? "";
    const activity = item.activity ?? item.Activity ?? (action ? humanizeAction(action) : "Activity recorded");
    const details = item.details ?? item.Details ?? item.detail ?? item.Detail ?? "";
    const category = item.category ?? item.Category ?? inferCategory(action);
    const at = item.at ?? item.At ?? "";
    return {
      id: item.id ?? item.Id ?? `${at}-${action}-${index}`,
      at,
      category: category || "Profile",
      action,
      activity,
      details: details || activity,
      subject: item.subject ?? item.Subject ?? subjectFallback,
      performedBy: item.performedBy ?? item.PerformedBy ?? null,
      oldValues: item.oldValues ?? item.OldValues ?? null,
      newValues: item.newValues ?? item.NewValues ?? null,
    };
  });
}

export function CoachHistory({ items, subject }: { items: HistoryInput[]; subject: string }) {
  const rows = useMemo(() => normalizeCoachHistory(items, subject), [items, subject]);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [category, setCategory] = useState<(typeof categories)[number]>("All");
  const [query, setQuery] = useState("");
  const [sortHeader, setSortHeader] = useState<string | null>("Date & Time");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("desc");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [selected, setSelected] = useState<CoachHistoryItem | null>(null);

  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const matched = rows.filter((item) => {
      if (category !== "All" && item.category !== category) {
        return false;
      }
      const day = dayKey(item.at);
      if (from && day && day < from) {
        return false;
      }
      if (to && day && day > to) {
        return false;
      }
      if (!needle) {
        return true;
      }
      return [item.activity, item.details, item.performedBy, item.category, item.subject, item.action]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(needle));
    });

    const header = sortHeader;
    if (!header) {
      return matched;
    }
    const direction = sortDirection === "asc" ? 1 : -1;
    return [...matched].sort((left, right) => {
      const pair = header === "Date & Time"
        ? [left.at, right.at]
        : header === "Activity"
          ? [left.activity, right.activity]
          : header === "Details"
            ? [left.details, right.details]
            : header === "Performed By"
              ? [left.performedBy ?? "", right.performedBy ?? ""]
              : [left.category, right.category];
      return pair[0].localeCompare(pair[1], undefined, { numeric: true, sensitivity: "base" }) * direction;
    });
  }, [rows, category, from, to, query, sortHeader, sortDirection]);

  const pageRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  function sort(header: string) {
    if (sortHeader === header) {
      setSortDirection((current) => (current === "asc" ? "desc" : "asc"));
      return;
    }
    setSortHeader(header);
    setSortDirection(header === "Date & Time" ? "desc" : "asc");
  }

  return (
    <div className="mt-4 grid gap-4">
      <div className="rounded-2xl border border-line bg-background p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h3 className="text-base font-semibold">Trainer activity history</h3>
            <p className="mt-1 max-w-3xl text-sm text-muted">
              Only activities for <span className="font-semibold text-foreground">{subject}</span>. The performer is the person or system that made the change.
            </p>
          </div>
        </div>
        <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <label className="grid gap-1 text-sm">From
            <input aria-label="History from date" className="field" onChange={(event) => { setFrom(event.target.value); setPage(1); }} type="date" value={from} />
          </label>
          <label className="grid gap-1 text-sm">To
            <input aria-label="History to date" className="field" onChange={(event) => { setTo(event.target.value); setPage(1); }} type="date" value={to} />
          </label>
          <label className="grid gap-1 text-sm">Category
            <select aria-label="History category" className="field" onChange={(event) => { setCategory(event.target.value as (typeof categories)[number]); setPage(1); }} value={category}>
              {categories.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </label>
          <label className="grid gap-1 text-sm">Search
            <input aria-label="Search history" className="field" onChange={(event) => { setQuery(event.target.value); setPage(1); }} placeholder="Activity, details, performer" value={query} />
          </label>
        </div>
      </div>

      <DataTable
        columns={[
          { header: "Date & Time", cell: (row) => formatWhen(row.at), value: (row) => row.at },
          { header: "Activity", cell: (row) => row.activity, value: (row) => row.activity },
          { header: "Details", cell: (row) => <span className="line-clamp-2 max-w-md">{row.details}</span>, value: (row) => row.details },
          { header: "Performed By", cell: (row) => row.performedBy || "System", value: (row) => row.performedBy || "System" },
          {
            header: "Category",
            cell: (row) => <span className="inline-flex min-h-8 items-center rounded-full border border-line px-3 text-xs font-semibold">{row.category}</span>,
            value: (row) => row.category,
          },
        ]}
        empty="No trainer activity matches these filters."
        onRowClick={(row) => setSelected(row)}
        paging={{
          page,
          pageSize,
          totalCount: filtered.length,
          onPageChange: setPage,
          onPageSizeChange: (size) => { setPageSize(size); setPage(1); },
        }}
        rowKey={(row) => row.id}
        rows={pageRows}
        sorting={{
          header: sortHeader,
          direction: sortDirection,
          sortable: ["Date & Time", "Activity", "Details", "Performed By", "Category"],
          onSort: sort,
        }}
      />

      {selected ? (
        <FormDialog onCancel={() => setSelected(null)} title="Activity details" wide>
          <div className="mt-4 grid gap-4 text-sm">
            <Detail label="Subject (trainer)" value={selected.subject} />
            <Detail label="Performed by" value={selected.performedBy || "System"} />
            <Detail label="Date & time" value={formatWhen(selected.at)} />
            <Detail label="Category" value={selected.category} />
            <Detail label="Activity" value={selected.activity} />
            <Detail label="Details" value={selected.details} />
            <Detail label="Previous value" value={selected.oldValues || "—"} />
            <Detail label="New value" value={selected.newValues || "—"} />
            <div className="flex justify-end">
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={() => setSelected(null)} type="button">Close</button>
            </div>
          </div>
        </FormDialog>
      ) : null}
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid gap-1 rounded-xl border border-line bg-background px-4 py-3">
      <p className="text-muted">{label}</p>
      <p className="font-medium break-words">{value}</p>
    </div>
  );
}
