import { describe, expect, it } from "vitest";
import { nameInitials } from "./initials";

describe("nameInitials", () => {
  it("uses the first and last name", () => {
    expect(nameInitials("John", "Kumar")).toBe("JK");
    expect(nameInitials("Mary Ann", "Smith")).toBe("MS");
  });

  it("handles a single name, a missing last name, and a blank name", () => {
    expect(nameInitials("John", "")).toBe("JO");
    expect(nameInitials("", "Kumar")).toBe("KU");
    expect(nameInitials(" ", null)).toBe("?");
  });
});
