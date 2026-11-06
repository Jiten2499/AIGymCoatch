import { describe, expect, it } from "vitest";
import { systemJobActions } from "./systemJobs";

describe("system job actions", () => {
  it("shows enable only when a manager is looking at a disabled job", () => {
    expect(systemJobActions(false, true)).toEqual({ enable: true, disable: false, run: false });
    expect(systemJobActions(true, true)).toEqual({ enable: false, disable: true, run: true });
    expect(systemJobActions(true, false)).toEqual({ enable: false, disable: false, run: false });
  });
});
