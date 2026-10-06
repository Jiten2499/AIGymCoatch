import { getSession, setSession, type AdminSession } from "./session";

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

const root = process.env.NEXT_PUBLIC_API_URL ?? "http://127.0.0.1:8080/api/v1";

export function apiRoot(): string {
  return root;
}

export async function api<T>(path: string, init: RequestInit = {}, allowRefresh = true): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (init.body) {
    headers.set("Content-Type", "application/json");
  }
  const session = getSession();
  if (session) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  const response = await fetch(`${root}${path}`, { ...init, headers });
  if (response.status === 401 && allowRefresh && session) {
    const refreshed = await refresh(session.refreshToken);
    if (refreshed) {
      return api<T>(path, init, false);
    }
  }

  if (!response.ok) {
    throw new ApiError(await problemDetail(response), response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export async function login(email: string, password: string): Promise<AdminSession> {
  const response = await fetch(`${root}/auth/login`, {
    method: "POST",
    headers: { Accept: "application/json", "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!response.ok) {
    throw new ApiError(await problemDetail(response), response.status);
  }
  const body = (await response.json()) as AdminSession;
  setSession(body);
  return body;
}

async function refresh(refreshToken: string): Promise<boolean> {
  const response = await fetch(`${root}/auth/refresh`, {
    method: "POST",
    headers: { Accept: "application/json", "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
  if (!response.ok) {
    setSession(null);
    return false;
  }
  setSession((await response.json()) as AdminSession);
  return true;
}

async function problemDetail(response: Response): Promise<string> {
  const body = (await response.json().catch(() => null)) as { detail?: string; title?: string } | null;
  if (response.status === 409) {
    return body?.detail ?? "No licenses are currently available.";
  }
  if (response.status === 403) {
    return body?.detail ?? "You do not have access to platform administration.";
  }
  return body?.detail ?? body?.title ?? "The request could not be completed.";
}
