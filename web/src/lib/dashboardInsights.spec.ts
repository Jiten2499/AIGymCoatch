import { describe, expect, it } from "vitest";
import { dashboardInsightsQueryKey, insightsPath, sameTenantInsight } from "./dashboardInsights";

describe("dashboard insights client", () => {
  it("keeps the tenant and range in the query identity", () => {
    expect(dashboardInsightsQueryKey("gym-a", "2026-09-06", "2026-10-05", "Day", 0)).toEqual([
      "business-dashboard-insights",
      "gym-a",
      "2026-09-06",
      "2026-10-05",
      "Day",
      0,
    ]);
    expect(insightsPath("2026-09-06", "2026-10-05", "Day", true)).toBe("/business/dashboard/insights?from=2026-09-06&to=2026-10-05&period=Day&refresh=true");
  });

  it("does not reuse another tenant's insights", () => {
    const report = { attention: [], ai: { status: "Ready", summary: "Gym A", insights: [], recommendations: [] } };
    expect(sameTenantInsight("gym-a", "gym-a", report)?.ai.summary).toBe("Gym A");
    expect(sameTenantInsight("gym-a", "gym-b", report)).toBeUndefined();
  });
});
