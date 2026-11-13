import { afterEach, describe, expect, it, vi } from "vitest";
import { api, ApiError, login, refreshSession } from "./api";
import { getSession, isIdle, setSession, subscribeSession, touchActivity, type AdminSession } from "./session";

describe("admin api", () => {
  afterEach(() => {
    setSession(null);
    vi.unstubAllGlobals();
  });

  it("keeps the session in memory", () => {
    setSession(sampleSession());
    expect(getSession()?.accessToken).toBe("access");
    setSession(null);
    expect(getSession()).toBeNull();
  });

  it("restores a saved session after a refresh", async () => {
    const memory = new Map<string, string>();
    vi.stubGlobal("window", {
      localStorage: {
        getItem: (key: string) => memory.get(key) ?? null,
        setItem: (key: string, value: string) => memory.set(key, value),
        removeItem: (key: string) => memory.delete(key),
      },
    });
    vi.resetModules();
    memory.set("lifefit.session", JSON.stringify(sampleSession()));
    const session = await import("./session");
    const restored: { current: AdminSession | null } = { current: null };
    session.subscribeSession(() => {
      restored.current = session.getSession();
    });
    await Promise.resolve();
    expect(restored.current?.accessToken).toBe("access");
    session.setSession(null);
    expect(memory.has("lifefit.session")).toBe(false);
  });

  it("maps a forbidden response without reading a secret", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ detail: "You do not have access to platform administration." }), { status: 403 })));
    await expect(login("ada@example.com", "secret")).rejects.toBeInstanceOf(ApiError);
  });

  it("shares one refresh when several requests expire together", async () => {
    const memory = new Map<string, string>();
    stubStorage(memory);
    setSession(sampleSession("2000-01-01T00:00:00Z"));
    touchActivity();
    const fetchMock = vi.fn(async (url: string) => {
      if (String(url).endsWith("/auth/refresh")) {
        return new Response(JSON.stringify(sampleSession()), { status: 200 });
      }
      return new Response("{}", { status: 200 });
    });
    vi.stubGlobal("fetch", fetchMock);
    await Promise.all([api("/features"), api("/approvals/count")]);
    expect(fetchMock.mock.calls.filter((call) => String(call[0]).endsWith("/auth/refresh"))).toHaveLength(1);
    expect(getSession()?.accessToken).toBe("access");
  });

  it("renews the login token without redrawing the open page", async () => {
    const memory = new Map<string, string>();
    stubStorage(memory);
    setSession(sampleSession("2000-01-01T00:00:00Z"));
    touchActivity();
    let notices = 0;
    const stop = subscribeSession(() => {
      notices += 1;
    });
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(sampleSession("2099-01-01T00:00:00Z", "renewed", "next-refresh")), { status: 200 })));
    await expect(refreshSession()).resolves.toBe(true);
    expect(getSession()?.accessToken).toBe("renewed");
    expect(getSession()?.refreshToken).toBe("next-refresh");
    expect(notices).toBe(0);
    expect(JSON.parse(memory.get("lifefit.session") ?? "").accessToken).toBe("renewed");
    stop();
  });

  it("redraws the page when a renewed session changes the workspace", async () => {
    const memory = new Map<string, string>();
    stubStorage(memory);
    setSession({ ...sampleSession(), currentTenant: { id: "north", name: "North", type: "Business", status: "Active" } });
    touchActivity();
    let notices = 0;
    const stop = subscribeSession(() => {
      notices += 1;
    });
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({
      ...sampleSession("2099-01-01T00:00:00Z", "renewed"),
      currentTenant: { id: "south", name: "South", type: "Business", status: "Active" },
    }), { status: 200 })));
    await expect(refreshSession()).resolves.toBe(true);
    expect(notices).toBe(1);
    expect(getSession()?.currentTenant?.id).toBe("south");
    stop();
  });

  it("keeps the session when refresh cannot reach the server", async () => {
    const memory = new Map<string, string>();
    stubStorage(memory);
    setSession(sampleSession());
    touchActivity();
    vi.stubGlobal("fetch", vi.fn(async () => Promise.reject(new Error("offline"))));
    await expect(refreshSession()).resolves.toBe(false);
    expect(getSession()?.accessToken).toBe("access");
  });

  it("ends the session only after a long idle period", async () => {
    const memory = new Map<string, string>();
    stubStorage(memory);
    setSession(sampleSession());
    touchActivity(Date.now() - 9 * 60 * 60 * 1000);
    expect(isIdle()).toBe(true);
    await expect(api("/features")).rejects.toThrow("Your session ended after a period of inactivity.");
    expect(getSession()).toBeNull();
  });

  it("maps a seat conflict to the server message", async () => {
    setSession(sampleSession());
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ detail: "No licenses are currently available." }), { status: 409 })));
    await expect(api("/platform/tenants/tenant/license-assignments", { method: "POST", body: "{}" })).rejects.toThrow("No licenses are currently available.");
  });
});

function stubStorage(memory: Map<string, string>): void {
  vi.stubGlobal("window", {
    localStorage: {
      getItem: (key: string) => memory.get(key) ?? null,
      setItem: (key: string, value: string) => memory.set(key, value),
      removeItem: (key: string) => memory.delete(key),
    },
  });
}

function sampleSession(expiresAt = "2099-01-01T00:00:00Z", accessToken = "access", refreshToken = "refresh"): AdminSession {
  return { accessToken, refreshToken, expiresAt, user: { id: "1", email: "ada@example.com", firstName: "Ada", lastName: "Lovelace" } };
}
