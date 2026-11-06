import { describe, expect, it } from "vitest";
import { ApiError } from "./api";
import { dashboardProblem, summaryHighlights, visibleWidgets, widgetMetrics, type DashboardSummary } from "./businessDashboard";
import { resolveHome } from "./home";

describe("business dashboard foundation", () => {
  it("sends a platform administrator to the platform console", () => {
    const home = resolveHome([
      { id: "platform", name: "LifeFit AI", type: "Platform", status: "Active", roles: ["SuperAdmin"] },
      { id: "gym", name: "North", type: "Gym", status: "Active", roles: ["GymOwner"] },
    ]);
    expect(home).toEqual({ href: "/admin", tenantId: null });
  });

  it("sends a business member to that workspace", () => {
    const home = resolveHome([
      { id: "platform", name: "LifeFit AI", type: "Platform", status: "Active", roles: ["PersonalMember"] },
      { id: "gym", name: "North", type: "Gym", status: "Active", roles: ["GymOwner"] },
    ]);
    expect(home).toEqual({ href: "/business", tenantId: "gym" });
  });

  it("sends a custom business role to that workspace", () => {
    const home = resolveHome([
      { id: "platform", name: "LifeFit AI", type: "Platform", status: "Active", roles: ["PersonalMember"] },
      { id: "gym", name: "North", type: "Gym", status: "Active", roles: ["Front desk lead"] },
    ]);
    expect(home).toEqual({ href: "/business", tenantId: "gym" });
  });

  it("shows only capabilities the workspace has enabled", () => {
    const widgets = visibleWidgets([
      { code: "Members", enabled: true },
      { code: "Reports", enabled: false },
    ]);
    expect(widgets.map((widget) => widget.code)).toEqual(["Members"]);
  });

  it("distinguishes suspended, unauthorized, and unavailable responses", () => {
    expect(dashboardProblem(new ApiError("This workspace is suspended.", 403))).toBe("suspended");
    expect(dashboardProblem(new ApiError("You do not have permission to perform this action.", 403))).toBe("unauthorized");
    expect(dashboardProblem(new ApiError("This dashboard is available for a business workspace.", 403))).toBe("unavailable");
  });

  it("renders summary numbers, zeros, and unavailable groups", () => {
    const summary: DashboardSummary = {
      members: { total: 3, active: 2, newThisMonth: 0 },
      attendance: { checkInsToday: 0 },
      memberships: null,
      people: null,
    };

    expect(summaryHighlights(summary).map((line) => `${line.label}:${line.value}`)).toEqual(["Active members:2", "Check-ins today:0"]);
    const members = widgetMetrics("Members", summary);
    expect(Array.isArray(members) ? members.map((line) => line.value) : members).toEqual([3, 2, 0]);
    expect(widgetMetrics("Memberships", summary)).toBe("unavailable");
    expect(widgetMetrics("Fitness", summary)).toBe("placeholder");
  });
});
