import { api } from "./api";
import type { AnalyticsPeriod } from "./dashboardAnalytics";

export type RevenuePoint = { periodStart: string; periodEnd: string; label: string; amount: number };
export type DashboardSession = { id: string; memberName: string; startUtc: string };
export type DashboardActivity = { kind: string; title: string; atUtc: string };
export type DashboardAlert = { kind: string; title: string; count: number };

export type DashboardBoard = {
  from: string;
  to: string;
  currency: string;
  activeMembers: number | null;
  paymentsCollected: number | null;
  checkInsToday: number | null;
  expiringSoon: number | null;
  inactiveMembers: number | null;
  overduePayments: number | null;
  leadsToFollowUp: number | null;
  revenue: RevenuePoint[] | null;
  sessions: DashboardSession[] | null;
  activities: DashboardActivity[] | null;
  alerts: DashboardAlert[] | null;
};

export type AttentionLink = { href: string; label: string; detail: string; count: number };

export function boardPath(from: string, to: string, period: AnalyticsPeriod): string {
  const query = new URLSearchParams({ from, to, period });
  return `/business/dashboard/board?${query.toString()}`;
}

export function getDashboardBoard(from: string, to: string, period: AnalyticsPeriod): Promise<DashboardBoard> {
  return api<DashboardBoard>(boardPath(from, to, period));
}

export function dashboardGreeting(displayName: string, currentDate: string): string {
  const parsed = new Date(currentDate);
  const hour = Number.isNaN(parsed.getTime()) ? 12 : parsed.getHours();
  const hello = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
  const first = displayName.trim().split(/\s+/).find(Boolean) ?? "there";
  return `${hello}, ${first}`;
}

export function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency, maximumFractionDigits: 0 }).format(amount);
  } catch {
    return `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(amount)} ${currency}`;
  }
}

export function revenueIsEmpty(points: RevenuePoint[] | null | undefined): boolean {
  return !points || points.every((point) => point.amount === 0);
}

export function attentionLinks(board: DashboardBoard): AttentionLink[] {
  const links: AttentionLink[] = [];
  if (board.expiringSoon !== null) {
    links.push({ href: "/business/memberships", label: "Expiring memberships", detail: "End within 7 days", count: board.expiringSoon });
  }
  if (board.overduePayments !== null) {
    links.push({ href: "/business/members", label: "Overdue payments", detail: "Balance still outstanding", count: board.overduePayments });
  }
  if (board.inactiveMembers !== null) {
    links.push({ href: "/business/members?status=Inactive", label: "Inactive members", detail: "Marked inactive", count: board.inactiveMembers });
  }
  if (board.leadsToFollowUp !== null) {
    links.push({ href: "/business/leads?followUp=today", label: "Leads to follow up", detail: "Due today or overdue", count: board.leadsToFollowUp });
  }
  return links;
}

export function quickActions(board: DashboardBoard): { href: string; label: string }[] {
  const actions: { href: string; label: string }[] = [];
  if (board.activeMembers !== null) {
    actions.push({ href: "/business/members?action=add", label: "Add member" });
  }
  if (board.paymentsCollected !== null) {
    actions.push({ href: "/business/members", label: "Record payment" });
  }
  if (board.checkInsToday !== null) {
    actions.push({ href: "/business/members", label: "Record attendance" });
  }
  if (board.leadsToFollowUp !== null) {
    actions.push({ href: "/business/leads?action=add", label: "Add lead" });
  }
  return actions;
}
