import { describe, expect, it } from "vitest";
import { convertStepError, draftFromLead } from "./convertWizard";

describe("convert lead wizard", () => {
  it("requires member basics before matching", () => {
    const draft = draftFromLead({ fullName: "Rahul", mobile: "", email: null, source: "WalkIn", notes: null }, "2026-10-07");
    expect(convertStepError(1, draft)).toBe("Enter a first and last name.");
    draft.fullName = "Rahul Shah";
    expect(convertStepError(1, draft)).toBe("Enter a mobile number.");
    draft.phone = "9876543210";
    expect(convertStepError(1, draft)).toBeNull();
  });

  it("requires a member choice when matches are shown and allows no package", () => {
    const draft = draftFromLead({ fullName: "Rahul Shah", mobile: "9876543210", email: null, source: "WalkIn", notes: null }, "2026-10-07");
    draft.createNew = false;
    draft.memberProfileId = null;
    expect(convertStepError(2, draft)).toBe("Choose an existing member or create a new one.");
    draft.createNew = true;
    expect(convertStepError(2, draft)).toBeNull();
    draft.planId = "plan-1";
    draft.startDate = "";
    expect(convertStepError(3, draft)).toBe("Enter a membership start date.");
    draft.startDate = "2026-10-07";
    expect(convertStepError(3, draft)).toBeNull();
  });

  it("allows basic LifeFit login without a plan seat", () => {
    const draft = draftFromLead({ fullName: "Rahul Shah", mobile: "9876543210", email: null, source: "WalkIn", notes: null }, "2026-10-07");
    expect(draft.enableBasicAppAccess).toBe(false);
    expect(draft.assignAppAccess).toBe(false);
    expect(convertStepError(3, draft)).toBeNull();
    draft.enableBasicAppAccess = true;
    expect(convertStepError(3, draft)).toBe("Enter an email for LifeFit app login.");
    draft.email = "rahul@gym.test";
    expect(convertStepError(3, draft)).toBeNull();
    draft.assignAppAccess = true;
    expect(convertStepError(3, draft)).toBe("Select a LifeFit app plan.");
    draft.appSubscriptionId = "sub-1";
    expect(convertStepError(3, draft)).toBeNull();
  });
});
