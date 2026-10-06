"use client";

import { useMemo, useState } from "react";

export type Column<T> = {
  header: string;
  cell: (row: T) => React.ReactNode;
  value?: (row: T) => string | number | null;
};

type Sort = { header: string; direction: "asc" | "desc" };

const pageSizes = [10, 20, 50];

function columnText<T>(column: Column<T>, row: T) {
  if (column.value) {
    return String(column.value(row) ?? "");
  }
  const rendered = column.cell(row);
  return typeof rendered === "string" || typeof rendered === "number" ? String(rendered) : "";
}

export function DataTable<T>({
  columns,
  rows,
  empty,
  rowKey,
  selection,
}: {
  columns: Column<T>[];
  rows: T[];
  empty: string;
  rowKey: (row: T) => string;
  selection?: {
    isSelected: (row: T) => boolean;
    onToggle: (row: T) => void;
    label: (row: T) => string;
    onToggleVisible?: (rows: T[]) => void;
  };
}) {
  const [filter, setFilter] = useState("");
  const [sort, setSort] = useState<Sort | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const filtered = useMemo(() => {
    const query = filter.trim().toLowerCase();
    const matched = rows.filter((row) => query.length === 0 || columns.some((column) => columnText(column, row).toLowerCase().includes(query)));
    if (!sort) {
      return matched;
    }
    const column = columns.find((item) => item.header === sort.header);
    if (!column) {
      return matched;
    }
    return [...matched].sort((left, right) => {
      const compared = columnText(column, left).localeCompare(columnText(column, right), undefined, { numeric: true, sensitivity: "base" });
      return sort.direction === "asc" ? compared : -compared;
    });
  }, [columns, filter, rows, sort]);
  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentPage = Math.min(page, pageCount);
  const start = (currentPage - 1) * pageSize;
  const visible = filtered.slice(start, start + pageSize);
  const pageSelected = selection?.onToggleVisible !== undefined && visible.length > 0 && visible.every((row) => selection.isSelected(row));

  if (rows.length === 0) {
    return <p className="rounded-2xl border border-line bg-surface p-6 text-muted">{empty}</p>;
  }

  return (
    <div className="grid gap-3">
      <input aria-label="Filter rows" className="field max-w-sm" onChange={(event) => { setFilter(event.target.value); setPage(1); }} placeholder="Filter" value={filter} />
      <div className="overflow-x-auto rounded-2xl border border-line">
        <table className="min-w-full text-left text-sm">
          <thead className="bg-surface text-muted">
            <tr>
              {selection ? (
                <th className="w-12 px-4 py-3" scope="col">
                  {selection.onToggleVisible ? (
                    <input aria-label="Select all rows on this page" checked={pageSelected} className="size-4" onChange={() => selection.onToggleVisible?.(visible)} type="checkbox" />
                  ) : (
                    <span className="sr-only">Select</span>
                  )}
                </th>
              ) : null}
              {columns.map((column) => (
                <th aria-sort={sort?.header === column.header ? (sort.direction === "asc" ? "ascending" : "descending") : "none"} className="px-4 py-3 font-medium" key={column.header} scope="col">
                  <button className="min-h-11" onClick={() => setSort(sort?.header === column.header && sort.direction === "asc" ? { header: column.header, direction: "desc" } : { header: column.header, direction: "asc" })} type="button">
                    {column.header}
                  </button>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {visible.length === 0 ? (
              <tr className="border-t border-line">
                <td className="px-4 py-6 text-muted" colSpan={columns.length + (selection ? 1 : 0)}>No rows match the current filters.</td>
              </tr>
            ) : visible.map((row) => (
              <tr className="border-t border-line" key={rowKey(row)}>
                {selection ? (
                  <td className="px-4 py-3">
                    <input aria-label={selection.label(row)} checked={selection.isSelected(row)} className="size-4" onChange={() => selection.onToggle(row)} type="checkbox" />
                  </td>
                ) : null}
                {columns.map((column) => (
                  <td className="px-4 py-3 align-top" key={column.header}>{column.cell(row)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="flex flex-wrap items-center gap-3 text-sm">
        <span>{filtered.length === 0 ? "0 rows" : `${start + 1}–${Math.min(start + pageSize, filtered.length)} of ${filtered.length}`}</span>
        <label className="flex items-center gap-2">Rows
          <select aria-label="Rows per page" className="field" onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }} value={pageSize}>
            {pageSizes.map((size) => <option key={size} value={size}>{size}</option>)}
          </select>
        </label>
        <button className="min-h-11" disabled={currentPage <= 1} onClick={() => setPage(currentPage - 1)} type="button">Previous</button>
        <span>Page {currentPage} of {pageCount}</span>
        <button className="min-h-11" disabled={currentPage >= pageCount} onClick={() => setPage(currentPage + 1)} type="button">Next</button>
      </div>
    </div>
  );
}
