"use client";

import { useMemo, useState } from "react";
import { ScrollableGrid } from "./ScrollableGrid";

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
  className,
  paging,
  sorting,
  onRowClick,
}: {
  columns: Column<T>[];
  rows: T[];
  empty: string;
  rowKey: (row: T) => string;
  className?: string;
  selection?: {
    isSelected: (row: T) => boolean;
    onToggle: (row: T) => void;
    label: (row: T) => string;
    onToggleVisible?: (rows: T[]) => void;
  };
  onRowClick?: (row: T) => void;
  paging?: {
    page: number;
    pageSize: number;
    totalCount: number;
    onPageChange: (page: number) => void;
    onPageSizeChange: (pageSize: number) => void;
  };
  sorting?: {
    header: string | null;
    direction: "asc" | "desc";
    sortable: string[];
    onSort: (header: string) => void;
  };
}) {
  const [filter, setFilter] = useState("");
  const [sort, setSort] = useState<Sort | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const filtered = useMemo(() => {
    if (paging) {
      return rows;
    }
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
  }, [columns, filter, paging, rows, sort]);
  const pageCount = paging ? Math.max(1, Math.ceil(paging.totalCount / paging.pageSize)) : Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentPage = paging ? paging.page : Math.min(page, pageCount);
  const start = paging ? (paging.page - 1) * paging.pageSize : (currentPage - 1) * pageSize;
  const visible = paging ? filtered : filtered.slice(start, start + pageSize);
  const shownCount = paging ? paging.totalCount : filtered.length;
  const pageSelected = selection?.onToggleVisible !== undefined && visible.length > 0 && visible.every((row) => selection.isSelected(row));

  if (rows.length === 0) {
    return <p className={`rounded-2xl border border-line bg-surface p-6 text-muted ${className ?? ""}`}>{empty}</p>;
  }

  return (
    <div className={`grid min-w-0 gap-3 ${className ?? ""}`}>
      {paging ? null : <input aria-label="Filter rows" className="field max-w-sm" onChange={(event) => { setFilter(event.target.value); setPage(1); }} placeholder="Filter" value={filter} />}
      <ScrollableGrid className="rounded-2xl border border-line">
        <table className="w-max min-w-full text-left text-sm">
          <thead className="sticky top-0 z-10 bg-surface text-muted">
            <tr>
              {selection ? (
                <th className="sticky top-0 z-10 w-12 bg-surface px-4 py-3 whitespace-nowrap" scope="col">
                  {selection.onToggleVisible ? (
                    <input aria-label="Select all rows on this page" checked={pageSelected} className="size-4" onChange={() => selection.onToggleVisible?.(visible)} type="checkbox" />
                  ) : (
                    <span className="sr-only">Select</span>
                  )}
                </th>
              ) : null}
              {columns.map((column) => (
                <th aria-sort={(sorting ?? sort)?.header === column.header ? ((sorting ?? sort)?.direction === "asc" ? "ascending" : "descending") : "none"} className="sticky top-0 z-10 bg-surface px-4 py-3 font-medium whitespace-nowrap" key={column.header} scope="col">
                  <button className="min-h-11" disabled={sorting ? !sorting.sortable.includes(column.header) : false} onClick={() => (sorting ? sorting.onSort(column.header) : setSort(sort?.header === column.header && sort.direction === "asc" ? { header: column.header, direction: "desc" } : { header: column.header, direction: "asc" }))} type="button">
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
              <tr className={`border-t border-line ${onRowClick ? "cursor-pointer" : ""}`} key={rowKey(row)} onClick={onRowClick ? () => onRowClick(row) : undefined}>
                {selection ? (
                  <td className="px-4 py-3">
                    <input aria-label={selection.label(row)} checked={selection.isSelected(row)} className="size-4" onChange={() => selection.onToggle(row)} onClick={(event) => event.stopPropagation()} type="checkbox" />
                  </td>
                ) : null}
                {columns.map((column) => (
                  <td className="px-4 py-3 align-top whitespace-nowrap" key={column.header}>{column.cell(row)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </ScrollableGrid>
      <div className="flex flex-wrap items-center gap-3 text-sm">
        <span>{shownCount === 0 ? "0 rows" : `${start + 1}–${Math.min(start + (paging?.pageSize ?? pageSize), shownCount)} of ${shownCount}`}</span>
        <label className="flex items-center gap-2">Rows
          <select aria-label="Rows per page" className="field" onChange={(event) => { const size = Number(event.target.value); if (paging) { paging.onPageSizeChange(size); } else { setPageSize(size); setPage(1); } }} value={paging?.pageSize ?? pageSize}>
            {pageSizes.map((size) => <option key={size} value={size}>{size}</option>)}
          </select>
        </label>
        <button className="min-h-11" disabled={currentPage <= 1} onClick={() => (paging ? paging.onPageChange(currentPage - 1) : setPage(currentPage - 1))} type="button">Previous</button>
        <span>Page {currentPage} of {pageCount}</span>
        <button className="min-h-11" disabled={currentPage >= pageCount} onClick={() => (paging ? paging.onPageChange(currentPage + 1) : setPage(currentPage + 1))} type="button">Next</button>
      </div>
    </div>
  );
}
