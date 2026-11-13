import { describe, expect, it } from "vitest";
import { adminGroups } from "./adminNav";

describe("admin navigation", () => {
  it("hides background jobs until monitoring is allowed", () => {
    expect(adminGroups(false).some((group) => group.title === "System")).toBe(false);
  });

  it("keeps Google Drive with the other platform settings", () => {
    const settings = adminGroups(false).find((group) => group.title === "System Settings");
    expect(settings?.links).toEqual([
      ["Email & Delivery", "/admin/email"],
      ["Google Drive", "/admin/integrations"],
    ]);
  });

  it("places background jobs under System for an authorized operator", () => {
    const system = adminGroups(true).find((group) => group.title === "System");
    expect(system?.links).toEqual([["Background Jobs", "/admin/jobs"]]);
  });
});