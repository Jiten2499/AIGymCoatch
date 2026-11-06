import { describe, expect, it } from "vitest";
import { monthBounds, quarterBounds } from "./trainerPeriod";

describe("trainer periods", () => {
  it("uses a calendar month and quarter", () => {
    expect(monthBounds("2026-10")).toEqual({ from: "2026-10-01", to: "2026-10-31" });
    expect(quarterBounds("2026-11")).toEqual({ from: "2026-10-01", to: "2026-12-31" });
  });
});
