import { describe, expect, it } from "vitest";
import {
  certificationText,
  emptyCoachDraft,
  emptyCert,
  isValidPhone,
  resolvedFirstName,
  specializationText,
  stepError,
} from "./coachWizard";

describe("coachWizard", () => {
  it("requires personal identity fields on step 1", () => {
    const draft = emptyCoachDraft("2026-11-12");
    expect(stepError(1, draft)).toBe("Enter first and last name.");
    draft.firstName = "Ada";
    draft.lastName = "Lovelace";
    expect(stepError(1, draft)).toBe("Enter a valid email address.");
    draft.email = "ada@gym.test";
    expect(stepError(1, draft)).toContain("mobile number");
    draft.phone = "+977 9800000000";
    expect(stepError(1, draft)).toBeNull();
  });

  it("accepts Nepal-style phone numbers", () => {
    expect(isValidPhone("+977 9841234567")).toBe(true);
    expect(isValidPhone("9841234567")).toBe(true);
    expect(isValidPhone("abc")).toBe(false);
  });

  it("folds middle name into the first name payload", () => {
    const draft = emptyCoachDraft("2026-11-12");
    draft.firstName = "Ada";
    draft.middleName = "Byron";
    draft.lastName = "Lovelace";
    expect(resolvedFirstName(draft)).toBe("Ada Byron");
  });

  it("joins specialization chips and certification cards", () => {
    const draft = emptyCoachDraft("2026-11-12");
    draft.specializations = ["Strength Training", "Weight Loss"];
    expect(specializationText(draft)).toBe("Strength Training, Weight Loss");
    draft.certifications = [{ ...emptyCert(), name: "ACE CPT", organization: "ACE", issueDate: "2024-01-01" }];
    expect(certificationText(draft)).toContain("ACE CPT");
    expect(certificationText(draft)).toContain("Issued by ACE");
  });

  it("requires designation and joining date on later steps", () => {
    const draft = emptyCoachDraft("2026-11-12");
    draft.designation = "";
    expect(stepError(2, draft)).toBe("Choose a designation.");
    draft.designation = "Personal Trainer";
    draft.joiningDate = "";
    expect(stepError(3, draft)).toBe("Choose a joining date.");
  });

  it("requires a business seat on access when creating", () => {
    const draft = emptyCoachDraft("2026-11-12");
    expect(stepError(5, draft, { requireSeat: true })).toBe("Assign an available business seat.");
    draft.subscriptionId = "seat-1";
    expect(stepError(5, draft, { requireSeat: true })).toBeNull();
  });
});
