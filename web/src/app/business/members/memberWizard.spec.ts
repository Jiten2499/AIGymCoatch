import { describe, expect, it } from "vitest";
import { draftFromMember, editError, emptyDraft, availablePtSessions, memberUpdateBody, splitFullName, stepError } from "./memberWizard";
import type { GymMemberDetails } from "../../../lib/gym";

describe("add member wizard", () => {
  it("requires a full name and mobile before leaving basic information", () => {
    const draft = emptyDraft("2026-10-07");
    expect(stepError(1, draft)).toBe("Enter a first and last name.");
    draft.fullName = "Rahul";
    expect(stepError(1, draft)).toBe("Enter a first and last name.");
    draft.fullName = "Rahul Shah";
    expect(stepError(1, draft)).toBe("Enter a mobile number.");
    draft.phone = "9876543210";
    expect(stepError(1, draft)).toBeNull();
    expect(splitFullName(draft.fullName)).toEqual({ firstName: "Rahul", lastName: "Shah" });
  });

  it("lets contact stay empty and requires a package start only when a package is chosen", () => {
    const draft = emptyDraft("2026-10-07");
    draft.fullName = "Rahul Shah";
    draft.phone = "9876543210";
    expect(stepError(2, draft)).toBeNull();
    expect(stepError(3, draft)).toBeNull();
    draft.planId = "plan-1";
    draft.branchId = "";
    expect(stepError(4, draft)).toBeNull();
    draft.startDate = "";
    expect(stepError(4, draft)).toBe("Enter a membership start date.");
  });

  it("allows basic LifeFit login without a plan seat", () => {
    const draft = emptyDraft("2026-10-07");
    draft.joinDate = "2026-10-07";
    expect(draft.enableBasicAppAccess).toBe(false);
    expect(draft.assignAppAccess).toBe(false);
    expect(stepError(3, draft)).toBeNull();
    draft.enableBasicAppAccess = true;
    expect(stepError(3, draft)).toBe("Enter an email for LifeFit app login.");
    draft.email = "rahul@gym.test";
    expect(stepError(3, draft)).toBeNull();
    draft.assignAppAccess = true;
    expect(stepError(3, draft)).toBe("Select a LifeFit app plan.");
    draft.appSubscriptionId = "sub-1";
    expect(stepError(3, draft)).toBeNull();
  });

  it("requires a payment date and reference only when money is recorded", () => {
    const draft = emptyDraft("2026-10-07");
    draft.joinDate = "2026-10-07";
    draft.planId = "plan-1";
    draft.startDate = "2026-10-07";
    expect(stepError(4, draft)).toBeNull();
    draft.paymentAmount = "1500";
    draft.paymentDate = "";
    expect(stepError(4, draft)).toBe("Enter a payment date when recording a payment.");
    draft.paymentDate = "2026-10-07";
    draft.paymentMethod = "Upi";
    expect(stepError(4, draft)).toBe("Enter a reference number for this payment method.");
    draft.paymentReference = "UPI123";
    expect(stepError(4, draft)).toBeNull();
  });

  it("keeps membership and package choices when the step changes", () => {
    const draft = emptyDraft("2026-10-07");
    draft.fullName = "Rahul Shah";
    draft.trainerId = "trainer-1";
    draft.planId = "plan-1";
    draft.ptPackageId = "pt-1";
    expect(stepError(4, draft)).toBeNull();
    expect(draft.trainerId).toBe("trainer-1");
    expect(draft.planId).toBe("plan-1");
    expect(draft.ptPackageId).toBe("pt-1");
    expect(availablePtSessions(4, 10)).toEqual({ included: 4, standalone: 10, total: 14 });
    expect(availablePtSessions(0, null)).toEqual({ included: 0, standalone: 0, total: 0 });
  });

  it("keeps the saved profile fields when editing a member", () => {
    const member: GymMemberDetails = {
      id: "member-1",
      fullName: "Rahul Shah",
      mobile: "9876543210",
      hasPhoto: true,
      status: "Active",
      joinDate: "2026-10-01",
      email: "rahul@gym.test",
      firstName: "Rahul",
      lastName: "Shah",
      phone: "9876543210",
      memberCode: "M1",
      dateOfBirth: "1990-01-02",
      gender: "Male",
      address: "12 Lane",
      city: "Pune",
      state: "MH",
      postalCode: "411001",
      emergencyContactName: "Ana Shah",
      emergencyContactPhone: "9000000000",
      emergencyContactRelationship: "Sister",
      fitnessGoal: "Strength",
      assignedTrainerId: "trainer-1",
    };
    const draft = draftFromMember(member);
    expect(editError(draft)).toBeNull();
    expect(memberUpdateBody(draft)).toMatchObject({
      firstName: "Rahul",
      lastName: "Shah",
      phone: "9876543210",
      city: "Pune",
      state: "MH",
      postalCode: "411001",
      emergencyContactName: "Ana Shah",
      fitnessGoal: "Strength",
      assignedTrainerId: "trainer-1",
      status: "Active",
    });
    draft.fullName = "Rahul";
    expect(editError(draft)).toBe("Enter a first and last name.");
  });
});
