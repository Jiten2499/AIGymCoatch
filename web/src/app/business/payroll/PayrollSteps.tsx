"use client";

import type { PayrollStep } from "../../../lib/payroll";

const STEPS: { id: PayrollStep; label: string; hint: string }[] = [
  { id: "prepare", label: "Prepare", hint: "Select period & review data" },
  { id: "review", label: "Review", hint: "Check details & resolve issues" },
  { id: "approve", label: "Approve", hint: "Confirm and lock payroll" },
  { id: "pay", label: "Record Payment", hint: "Mark as paid & generate payslips" },
];

export function PayrollSteps({
  current,
  status,
  onSelect,
}: {
  current: PayrollStep;
  status?: string;
  onSelect: (step: PayrollStep) => void;
}) {
  const index = STEPS.findIndex((step) => step.id === current);
  return (
    <ol className="grid gap-2 rounded-2xl border border-line bg-surface p-3 sm:grid-cols-2 xl:grid-cols-4">
      {STEPS.map((step, position) => {
        const active = step.id === current;
        const done = position < index || status === "Paid";
        return (
          <li key={step.id}>
            <button
              className={`flex w-full items-start gap-3 rounded-xl px-3 py-3 text-left transition ${
                active ? "bg-accent/15 ring-1 ring-accent" : "hover:bg-background"
              }`}
              onClick={() => onSelect(step.id)}
              type="button"
            >
              <span
                className={`mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-sm font-semibold ${
                  done || active ? "bg-accent text-background" : "border border-line bg-background text-muted"
                }`}
              >
                {position + 1}
              </span>
              <span>
                <span className="block text-sm font-semibold">{step.label}</span>
                <span className="mt-0.5 block text-xs text-muted">{step.hint}</span>
              </span>
            </button>
          </li>
        );
      })}
    </ol>
  );
}
