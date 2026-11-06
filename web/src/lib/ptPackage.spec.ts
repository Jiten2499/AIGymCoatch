import { describe, expect, it } from "vitest";
import { noPtPackagesMessage, ptPackageLabel } from "./ptPackage";

describe("pt package options", () => {
  it("shows name, sessions, validity, and price", () => {
    expect(ptPackageLabel({ name: "Personal Training — 12 Sessions", sessionCount: 12, validityDays: 60, price: 6000, currency: "INR" })).toBe(
      "Personal Training — 12 Sessions · 12 sessions · 60 days · 6000 INR",
    );
  });

  it("explains an empty catalogue", () => {
    expect(noPtPackagesMessage).toContain("Configuration");
  });
});
