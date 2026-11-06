import { api } from "./api";

export type PayrollRunSummary = {
  id: string;
  periodYear: number;
  periodMonth: number;
  periodStart: string;
  periodEnd: string;
  currency: string;
  status: string;
  staffCount: number;
  grossPay: number;
  totalDeductions: number;
  netPayable: number;
  amountPaid: number;
  issueCount: number;
};

export type PayrollAdjustment = { id: string; kind: string; label: string; amount: number; reason: string };
export type PayrollPayment = { id: string; amount: number; paymentDate: string; method: string; reference: string | null };

export type PayrollLine = {
  id: string;
  staffProfileId: string;
  userId: string;
  employeeName: string;
  employeeCode: string;
  staffType: string;
  baseSalary: number;
  allowances: number;
  commissions: number;
  bonuses: number;
  deductions: number;
  grossPay: number;
  netPay: number;
  amountPaid: number;
  remaining: number;
  paymentStatus: string;
  missingSalary: boolean;
  unapprovedCommission: boolean;
  unconfirmedAttendance: boolean;
  locked: boolean;
  adjustments: PayrollAdjustment[];
  payments: PayrollPayment[];
};

export type PayrollRunDetails = PayrollRunSummary & {
  canEdit: boolean;
  canApprove: boolean;
  canPay: boolean;
  lines: PayrollLine[];
};

export type StaffCompensation = {
  staffProfileId: string;
  employeeName: string;
  employeeCode: string;
  baseSalary: number;
  allowances: number;
  currency: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  notes: string | null;
};

export type PayrollStep = "prepare" | "review" | "approve" | "pay";

export function money(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency, maximumFractionDigits: 2 }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

export function periodLabel(year: number, month: number): string {
  return new Date(Date.UTC(year, month - 1, 1)).toLocaleString(undefined, { month: "long", year: "numeric", timeZone: "UTC" });
}

export function stepFromStatus(status: string): PayrollStep {
  if (status === "InReview") return "review";
  if (status === "Approved" || status === "PartiallyPaid" || status === "Paid") return "pay";
  return "prepare";
}

export function listPayrollRuns() {
  return api<PayrollRunSummary[]>("/payroll/runs");
}

export function getPayrollRun(id: string) {
  return api<PayrollRunDetails>(`/payroll/runs/${id}`);
}

export function preparePayroll(year: number, month: number) {
  return api<PayrollRunDetails>("/payroll/runs/prepare", { method: "POST", body: JSON.stringify({ year, month }) });
}

export function submitPayroll(id: string) {
  return api<PayrollRunDetails>(`/payroll/runs/${id}/submit`, { method: "POST" });
}

export function draftPayroll(id: string) {
  return api<PayrollRunDetails>(`/payroll/runs/${id}/draft`, { method: "POST" });
}

export function approvePayroll(id: string) {
  return api<PayrollRunDetails>(`/payroll/runs/${id}/approve`, { method: "POST", body: JSON.stringify({ confirm: true }) });
}

export function addAdjustment(lineId: string, kind: "Bonus" | "Deduction", label: string, amount: number, reason: string) {
  return api<PayrollLine>(`/payroll/lines/${lineId}/adjustments`, { method: "POST", body: JSON.stringify({ kind, label, amount, reason }) });
}

export function recordPayment(lineId: string, amount: number, paymentDate: string, method: string, reference: string | null) {
  return api<PayrollLine>(`/payroll/lines/${lineId}/payments`, { method: "POST", body: JSON.stringify({ amount, paymentDate, method, reference }) });
}

export function bulkPay(runId: string, paymentDate: string, method: string, referencePrefix: string | null) {
  return api<PayrollRunDetails>(`/payroll/runs/${runId}/payments/bulk`, { method: "POST", body: JSON.stringify({ paymentDate, method, referencePrefix }) });
}

export function listCompensation() {
  return api<StaffCompensation[]>("/payroll/compensation");
}

export function saveCompensation(staffProfileId: string, baseSalary: number, allowances: number, effectiveFrom: string, notes: string | null) {
  return api<StaffCompensation>(`/payroll/compensation/${staffProfileId}`, { method: "PUT", body: JSON.stringify({ baseSalary, allowances, effectiveFrom, notes }) });
}

export function getPayslip(lineId: string) {
  return api(`/payroll/lines/${lineId}/payslip`);
}
