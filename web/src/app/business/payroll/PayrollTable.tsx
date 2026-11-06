"use client";

import { money, type PayrollLine } from "../../../lib/payroll";

export function PayrollTable({
  lines,
  selectedId,
  currency,
  page,
  pageSize,
  total,
  onSelect,
  onPage,
}: {
  lines: PayrollLine[];
  selectedId: string | null;
  currency: string;
  page: number;
  pageSize: number;
  total: number;
  onSelect: (id: string) => void;
  onPage: (page: number) => void;
}) {
  if (total === 0) {
    return <p className="rounded-xl border border-dashed border-line px-4 py-8 text-center text-sm text-muted">No staff lines for this period.</p>;
  }

  const start = (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, total);
  const pages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="grid gap-3">
      <div className="overflow-x-auto">
        <table className="min-w-full text-left text-sm">
          <thead className="border-b border-line text-muted">
            <tr>
              <th className="px-3 py-2 font-medium">Staff</th>
              <th className="px-3 py-2 font-medium">Role</th>
              <th className="px-3 py-2 font-medium">Base</th>
              <th className="px-3 py-2 font-medium">Additions</th>
              <th className="px-3 py-2 font-medium">Deductions</th>
              <th className="px-3 py-2 font-medium">Net</th>
              <th className="px-3 py-2 font-medium">Status</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line) => {
              const additions = line.allowances + line.commissions + line.bonuses;
              const selected = line.id === selectedId;
              const flagged = line.missingSalary || line.unapprovedCommission || line.unconfirmedAttendance;
              return (
                <tr
                  className={`cursor-pointer border-b border-line/70 ${selected ? "bg-accent/10" : "hover:bg-background"}`}
                  key={line.id}
                  onClick={() => onSelect(line.id)}
                >
                  <td className="px-3 py-3">
                    <div className="font-medium">{line.employeeName}</div>
                    <div className="text-xs text-muted">{line.employeeCode}</div>
                  </td>
                  <td className="px-3 py-3 whitespace-nowrap text-muted">{line.staffType}</td>
                  <td className="px-3 py-3 whitespace-nowrap">{money(line.baseSalary, currency)}</td>
                  <td className="px-3 py-3 whitespace-nowrap">{money(additions, currency)}</td>
                  <td className="px-3 py-3 whitespace-nowrap">{money(line.deductions, currency)}</td>
                  <td className="px-3 py-3 whitespace-nowrap font-medium">{money(line.netPay, currency)}</td>
                  <td className="px-3 py-3">
                    <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-medium ${flagged ? "bg-amber-500/15 text-amber-200" : "bg-accent/20 text-accent"}`}>
                      {flagged ? "Needs review" : line.paymentStatus === "Pending" && !line.locked ? "Ready" : line.paymentStatus}
                    </span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Showing {start}–{end} of {total} staff</p>
        <div className="flex gap-2">
          <button className="rounded-full border border-line px-3 py-1 disabled:opacity-40" disabled={page <= 1} onClick={() => onPage(page - 1)} type="button">Previous</button>
          <button className="rounded-full border border-line px-3 py-1 disabled:opacity-40" disabled={page >= pages} onClick={() => onPage(page + 1)} type="button">Next</button>
        </div>
      </div>
    </div>
  );
}
