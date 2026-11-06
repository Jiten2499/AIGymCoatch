import { describe, expect, it } from "vitest";
import { emptyStaffDraft, roleKind, splitFullName, stepError } from "./staffWizard";

describe("staff wizard", () => {
  it("requires identity fields on step 1", () => {
    const draft = emptyStaffDraft("2026-11-01");
    expect(stepError(1, draft, false)).toBe("Enter a first and last name.");
    draft.fullName = "Staff Two";
    expect(stepError(1, draft, false)).toBe("Enter a valid email address.");
    draft.email = "staff2@gym.test";
    expect(stepError(1, draft, false)).toBe("Enter a mobile number.");
    draft.phone = "9999999999";
    expect(stepError(1, draft, false)).toBeNull();
  });

  it("requires employment and seat fields later", () => {
    const draft = emptyStaffDraft("2026-11-01");
    draft.fullName = "Staff Two";
    draft.email = "staff2@gym.test";
    draft.phone = "9999999999";
    expect(stepError(2, draft, true)).toBe("Enter a designation / job title.");
    draft.designation = "Front desk";
    draft.joiningDate = "2026-11-01";
    expect(stepError(2, draft, true)).toBe("Choose a branch.");
    draft.branchId = "branch-1";
    expect(stepError(2, draft, true)).toBeNull();
    expect(stepError(3, draft, false)).toBe("Assign an available business seat.");
    expect(stepError(3, draft, false, { requireSeat: false })).toBeNull();
    draft.subscriptionId = "seat-1";
    expect(stepError(3, draft, false)).toBeNull();
  });

  it("classifies conditional role fields", () => {
    expect(roleKind("Trainer")).toBe("trainer");
    expect(roleKind("GymManager")).toBe("manager");
    expect(roleKind("Accountant")).toBe("accountant");
    expect(roleKind("Receptionist")).toBe("receptionist");
    expect(splitFullName("Ada Lovelace")).toEqual({ firstName: "Ada", lastName: "Lovelace" });
  });
});
