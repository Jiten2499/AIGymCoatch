import { describe, expect, it } from "vitest";
import { attentionLinks, dashboardGreeting, formatMoney, quickActions, revenueIsEmpty, type DashboardBoard } from "./dashboardBoard";

const board: DashboardBoard = {
  from: "2026-10-01",
  to: "2026-10-28",
  currency: "USD",
  activeMembers: 12,
  paymentsCollected: 0,
  checkInsToday: 3,
  expiringSoon: 2,
  inactiveMembers: 0,
  overduePayments: 1,
  leadsToFollowUp: 4,
  revenue: [{ periodStart: "2026-10-01", periodEnd: "2026-10-01", label: "Oct 1", amount: 0 }],
  sessions: [],
  activities: [],
  alerts: [],
};

describe("business dashboard board", () => {
  it("greets the signed-in person from the workspace clock", () => {
    expect(dashboardGreeting("Priya Shah", "2026-10-28T09:00:00")).toBe("Good morning, Priya");
    expect(dashboardGreeting("Priya Shah", "2026-10-28T18:00:00")).toBe("Good evening, Priya");
  });

  it("links attention counts to the existing screens and keeps a real zero", () => {
    expect(attentionLinks(board).map((item) => [item.label, item.count, item.href])).toEqual([
      ["Expiring memberships", 2, "/business/memberships"],
      ["Overdue payments", 1, "/business/members"],
      ["Inactive members", 0, "/business/members?status=Inactive"],
      ["Leads to follow up", 4, "/business/leads?followUp=today"],
    ]);
    expect(quickActions({ ...board, paymentsCollected: null, leadsToFollowUp: null }).map((item) => item.label)).toEqual(["Add member", "Record attendance"]);
  });

  it("treats an all-zero revenue series as no confirmed payments", () => {
    expect(revenueIsEmpty(board.revenue)).toBe(true);
    expect(revenueIsEmpty([{ ...board.revenue![0], amount: 25 }])).toBe(false);
    expect(formatMoney(1500, "USD")).toContain("1,500");
  });
});
