import { describe, expect, it } from "vitest";
import { canCancelCommunication, canEditCommunication, canPublishNow, formatInZone, formatWallClock, localDateTimeValue, toDateTimeLocal } from "./schedule";

describe("communication schedule", () => {
  it("sends the selected wall clock without converting it to the browser UTC instant", () => {
    expect(localDateTimeValue("2026-10-15T10:00")).toBe("2026-10-15T10:00:00");
    expect(localDateTimeValue("")).toBeNull();
  });

  it("previews the selected date in the tenant timezone label", () => {
    expect(formatWallClock("2026-10-15T10:00")).toContain("15");
    expect(formatWallClock("2026-10-15T10:00")).toContain("2026");
    expect(formatWallClock("2026-10-15T10:00")).toContain("10:00");
  });

  it("shows a stored UTC instant in the tenant timezone", () => {
    expect(formatInZone("2026-10-15T04:30:00Z", "Asia/Kolkata")).toContain("10:00");
    expect(toDateTimeLocal("2026-10-15T04:30:00Z", "Asia/Kolkata")).toBe("2026-10-15T10:00");
  });

  it("limits publish, edit, and cancel to the communication lifecycle", () => {
    expect(canPublishNow("Scheduled")).toBe(true);
    expect(canPublishNow("Published")).toBe(false);
    expect(canPublishNow("Archived")).toBe(false);
    expect(canEditCommunication("Scheduled")).toBe(true);
    expect(canEditCommunication("Published")).toBe(false);
    expect(canCancelCommunication("Scheduled")).toBe(true);
    expect(canCancelCommunication("Draft")).toBe(false);
  });
});