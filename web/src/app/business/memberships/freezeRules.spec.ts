import { describe, expect, it } from "vitest";
import { addDays, freezeFormError } from "./freezeRules";

describe("freeze dialog rules", () => {
  it("accepts a length inside the gym limits", () => {
    expect(freezeFormError(7, 1, 30)).toBeNull();
    expect(addDays("2026-10-31", 7)).toBe("2026-11-07");
  });

  it("rejects a length outside the limits", () => {
    expect(freezeFormError(0, 1, 30)).toBe("Enter the number of freeze days.");
    expect(freezeFormError(31, 1, 30)).toBe("Freeze length must be between 1 and 30 days.");
    expect(freezeFormError(7, 1, 0)).toBe("This gym does not allow a membership freeze.");
  });
});
