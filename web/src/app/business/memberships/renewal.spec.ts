import { describe, expect, it } from "vitest";
import { renewalStart } from "./renewal";

describe("renewalStart", () => {
  it("starts the day after a membership that has not ended", () => {
    expect(renewalStart("Active", "2026-10-31", "2026-10-09")).toBe("2026-11-01");
  });

  it("starts today when the membership has already ended", () => {
    expect(renewalStart("Expired", "2026-09-30", "2026-10-09")).toBe("2026-10-09");
    expect(renewalStart("Active", "2026-10-08", "2026-10-09")).toBe("2026-10-09");
  });

  it("does not propose a date for a cancelled, suspended, or pending membership", () => {
    expect(renewalStart("Cancelled", "2026-12-01", "2026-10-09")).toBeNull();
    expect(renewalStart("Suspended", "2026-12-01", "2026-10-09")).toBeNull();
    expect(renewalStart("Pending", "2026-12-01", "2026-10-09")).toBeNull();
  });

  it("keeps a frozen membership on the day after its end", () => {
    expect(renewalStart("Frozen", "2026-10-31", "2026-10-09")).toBe("2026-11-01");
  });
});
