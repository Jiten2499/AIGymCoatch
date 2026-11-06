import { describe, expect, it } from "vitest";
import { appAccessError, appAccessPayload, emptyAppAccess } from "./AppAccessFields";

describe("LifeFit app access fields", () => {
  it("defaults to no login and no plan seat", () => {
    const draft = emptyAppAccess();
    expect(draft.enableBasicAppAccess).toBe(false);
    expect(draft.assignAppAccess).toBe(false);
    expect(appAccessError(draft)).toBeNull();
    expect(appAccessPayload(draft)).toEqual({ enableBasicAppAccess: false, assignAppAccess: false, appSubscriptionId: null });
  });

  it("allows basic login without a plan seat", () => {
    const draft = { enableBasicAppAccess: true, assignAppAccess: false, appSubscriptionId: "" };
    expect(appAccessError(draft, "rahul@gym.test")).toBeNull();
    expect(appAccessPayload(draft)).toEqual({ enableBasicAppAccess: true, assignAppAccess: false, appSubscriptionId: null });
  });

  it("requires email for basic login and a plan when a seat is requested", () => {
    const draft = { enableBasicAppAccess: true, assignAppAccess: true, appSubscriptionId: "" };
    expect(appAccessError(draft, "")).toBe("Enter an email for LifeFit app login.");
    draft.appSubscriptionId = "sub-1";
    expect(appAccessError(draft, "")).toBe("Enter an email for LifeFit app login.");
    expect(appAccessError(draft, "rahul@gym.test")).toBeNull();
    expect(appAccessPayload(draft)).toEqual({ enableBasicAppAccess: true, assignAppAccess: true, appSubscriptionId: "sub-1" });
  });
});
