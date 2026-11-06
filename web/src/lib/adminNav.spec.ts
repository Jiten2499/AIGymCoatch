import { describe, expect, it } from "vitest";
import { adminGroups } from "./adminNav";

describe("admin navigation", () => {
  it("hides background jobs until monitoring is allowed", () => {
    expect(adminGroups(false).some((group) => group.title === "System")).toBe(false);
  });

  it("places background jobs under System for an authorized operator", () => {
    const system = adminGroups(true).find((group) => group.title === "System");
    expect(system?.links).toEqual([["Background Jobs", "/admin/jobs"]]);
  });
});