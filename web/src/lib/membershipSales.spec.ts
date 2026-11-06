import { describe, expect, it } from "vitest";
import { formatSalesPercent, salesPeriodLabel, salesRange, salesTooltip } from "./membershipSales";

describe("membership sales distribution", () => {
  it("uses the current calendar month when the dashboard has no range", () => {
    expect(salesRange(null, "2026-10-28T18:30:00")).toEqual({ from: "2026-10-01", to: "2026-10-28", period: "Day" });
  });

  it("keeps the dashboard date filter when one is selected", () => {
    const range = { from: "2026-10-01", to: "2026-10-07", period: "Day" as const };
    expect(salesRange(range, "2026-10-28")).toEqual(range);
  });

  it("describes the hovered share with the package, percentage, and count", () => {
    expect(salesTooltip({ planId: "plan", packageName: "Monthly", sold: 2, percentage: 66.7 })).toBe("Monthly: 66.7% · 2 sold");
    expect(formatSalesPercent(100)).toBe("100%");
    expect(salesPeriodLabel("2026-10-01", "2026-10-28")).toContain("2026");
  });
});
