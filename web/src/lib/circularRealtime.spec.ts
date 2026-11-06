import { describe, expect, it } from "vitest";
import { acceptCircularEvent } from "./circularRealtime";

describe("circular realtime", () => {
  it("accepts a notice only for the signed-in tenant", () => {
    expect(acceptCircularEvent({ communicationId: "c1", tenantId: "tenant", audience: "AllStaff" }, "tenant")).toBe(true);
    expect(acceptCircularEvent({ communicationId: "c1", tenantId: "other" }, "tenant")).toBe(false);
    expect(acceptCircularEvent({ tenantId: "tenant" }, "tenant")).toBe(false);
    expect(acceptCircularEvent(null, null)).toBe(false);
  });
});