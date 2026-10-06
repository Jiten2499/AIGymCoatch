import { describe, expect, it } from "vitest";
import { analyticsIsEmpty, analyticsPath, customRange, dashboardAnalyticsQueryKey, formatAnalyticsLabel, presetRange } from "./dashboardAnalytics";

describe("dashboard analytics client", () => {
  it("keeps the workspace in the query key and sends the chart range", () => {
    expect(dashboardAnalyticsQueryKey("gym", "members", "2026-10-01", "2026-10-07", "Week")).toEqual([
      "business-dashboard-analytics",
      "gym",
      "members",
      "2026-10-01",
      "2026-10-07",
      "Week",
    ]);
    expect(analyticsPath("attendance", "2026-10-01", "2026-10-07", "Day")).toBe(
      "/business/dashboard/analytics/attendance?from=2026-10-01&to=2026-10-07&period=Day",
    );
  });

  it("maps presets, keeps zero points, and rejects an oversized range", () => {
    expect(presetRange("last30", "2026-10-05")).toEqual({ from: "2026-09-06", to: "2026-10-05", period: "Day" });
    expect(presetRange("last90", "2026-10-05").period).toBe("Week");
    expect(customRange("2026-01-01", "2030-01-01")).toBeNull();
    expect(formatAnalyticsLabel("2026-10-02", "2026-10-02", "Day")).toBe("Oct 02");
    expect(analyticsIsEmpty({
      from: "2026-10-01",
      to: "2026-10-02",
      period: "Day",
      series: [{ key: "checkIns", label: "Check-ins", points: [
        { periodStart: "2026-10-01", periodEnd: "2026-10-01", label: "2026-10-01", value: 0 },
        { periodStart: "2026-10-02", periodEnd: "2026-10-02", label: "2026-10-02", value: 0 },
      ] }],
    })).toBe(true);
  });
});
