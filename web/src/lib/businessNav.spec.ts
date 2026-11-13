import { describe, expect, it } from "vitest";
import { canSeeBusinessLink, gymLinks } from "./businessNav";

describe("business navigation", () => {
  it("exposes the gym operations added for a business workspace", () => {
    expect(gymLinks.map(([, href]) => href)).toEqual([
      "/business/members",
      "/business/memberships",
      "/business/leads",
      "/business/expenses",
      "/business/ledger",
      "/business/payroll",
      "/business/leaves",
      "/business/communications",
      "/business/chats",
      "/business/configuration",
    ]);
  });

  it("hides payroll and chat when the person cannot use them", () => {
    const features = new Map([["GYM_STAFF_PAYROLL", true], ["GYM_CHAT", true], ["BUSINESS_REPORTS", true], ["STAFF_LEAVE", true]]);
    const permissions = new Set(["REPORT_READ", "CHAT_READ"]);

    expect(canSeeBusinessLink("/business/payroll", permissions, features)).toBe(false);
    expect(canSeeBusinessLink("/business/chats", permissions, features)).toBe(true);
    expect(canSeeBusinessLink("/business/chats", new Set(["REPORT_READ"]), features)).toBe(false);
    expect(canSeeBusinessLink("/business", permissions, features)).toBe(true);
    expect(canSeeBusinessLink("/business", new Set(["MEMBER_READ"]), new Map())).toBe(true);
    expect(canSeeBusinessLink("/business/members", new Set(["MEMBER_READ"]), new Map())).toBe(true);
    expect(canSeeBusinessLink("/business/coaches", new Set(["MEMBER_READ"]), new Map())).toBe(false);
    expect(canSeeBusinessLink("/business/coaches", new Set(["TRAINER_READ"]), new Map())).toBe(true);
    expect(canSeeBusinessLink("/business/payroll", null, features)).toBe(false);
    expect(canSeeBusinessLink("/business/leaves", new Set(["LEAVE_APPLY"]), features)).toBe(true);
    expect(canSeeBusinessLink("/business/leaves", new Set(["LEAVE_READ"]), features)).toBe(true);
    expect(canSeeBusinessLink("/business/leaves", new Set(["REPORT_READ"]), features)).toBe(false);
  });
});
