import { describe, expect, it } from "vitest";
import { displayWorkspaceRole, workspaceRoleLabel } from "./workspaceRole";

describe("workspaceRole", () => {
  it("prefers custom business roles", () => {
    expect(displayWorkspaceRole(["Member", "Front desk lead"])).toBe("Front desk lead");
  });

  it("falls back to standard staff roles with labels", () => {
    expect(displayWorkspaceRole(["Member", "GymOwner"])).toBe("GymOwner");
    expect(workspaceRoleLabel("GymOwner")).toBe("Business Owner");
    expect(workspaceRoleLabel("GymManager")).toBe("Manager");
  });
});
