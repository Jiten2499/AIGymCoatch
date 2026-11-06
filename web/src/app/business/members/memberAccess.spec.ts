import { describe, expect, it } from "vitest";
import { canReadPayments, canRecordPtSession, canUpdateMember, memberDetailsHref } from "./memberAccess";

describe("member details access", () => {
  it("opens the selected member in the business tenant", () => {
    expect(memberDetailsHref("member-1")).toBe("/business/members/member-1");
  });

  it("lets tenant admins manage members and keeps trainers on read-only access", () => {
    expect(canUpdateMember(["GymOwner"])).toBe(true);
    expect(canUpdateMember(["Receptionist"])).toBe(true);
    expect(canUpdateMember(["Trainer"])).toBe(false);
    expect(canRecordPtSession(["GymManager"])).toBe(true);
    expect(canRecordPtSession(["Receptionist"])).toBe(false);
    expect(canRecordPtSession(["Trainer"])).toBe(false);
    expect(canReadPayments(["Trainer"])).toBe(false);
    expect(canReadPayments(["GymOwner"])).toBe(true);
  });
});
