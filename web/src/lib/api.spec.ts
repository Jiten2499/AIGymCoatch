import { afterEach, describe, expect, it, vi } from "vitest";
import { api, ApiError, login } from "./api";
import { getSession, setSession } from "./session";

describe("admin api", () => {
  afterEach(() => {
    setSession(null);
    vi.unstubAllGlobals();
  });

  it("keeps the session in memory", () => {
    setSession({ accessToken: "access", refreshToken: "refresh", expiresAt: "2099-01-01T00:00:00Z", user: { id: "1", email: "ada@example.com", firstName: "Ada", lastName: "Lovelace" } });
    expect(getSession()?.accessToken).toBe("access");
    setSession(null);
    expect(getSession()).toBeNull();
  });

  it("maps a forbidden response without reading a secret", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ detail: "You do not have access to platform administration." }), { status: 403 })));
    await expect(login("ada@example.com", "secret")).rejects.toBeInstanceOf(ApiError);
  });

  it("maps a seat conflict to the server message", async () => {
    setSession({ accessToken: "access", refreshToken: "refresh", expiresAt: "2099-01-01T00:00:00Z", user: { id: "1", email: "ada@example.com", firstName: "Ada", lastName: "Lovelace" } });
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ detail: "No licenses are currently available." }), { status: 409 })));
    await expect(api("/platform/tenants/tenant/license-assignments", { method: "POST", body: "{}" })).rejects.toThrow("No licenses are currently available.");
  });
});
