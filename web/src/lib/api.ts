import { getSession, isIdle, setSession, type AdminSession, type AdminUser } from "./session";

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

const root = process.env.NEXT_PUBLIC_API_URL ?? "http://127.0.0.1:8080/api/v1";
const renewLeadMs = 2 * 60 * 1000;
let pendingRefresh: Promise<boolean> | null = null;

export function apiRoot(): string {
  return root;
}

export function refreshSession(): Promise<boolean> {
  const session = getSession();
  if (!session) {
    return Promise.resolve(false);
  }

  if (isIdle()) {
    setSession(null);
    return Promise.resolve(false);
  }

  pendingRefresh ??= refresh(session.refreshToken).finally(() => {
    pendingRefresh = null;
  });
  return pendingRefresh;
}

export async function api<T>(path: string, init: RequestInit = {}, allowRefresh = true): Promise<T> {
  let session = getSession();
  if (session && isIdle()) {
    setSession(null);
    throw new ApiError("Your session ended after a period of inactivity.", 401);
  }

  if (session && allowRefresh && tokenExpiring(session)) {
    await refreshSession();
    session = getSession();
  }

  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (init.body && !(init.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }
  if (session) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  const response = await fetch(`${root}${path}`, { ...init, headers });
  if (response.status === 401 && allowRefresh && session) {
    const refreshed = await refreshSession();
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
  try {
    const response = await fetch(`${root}/auth/refresh`, {
      method: "POST",
      headers: { Accept: "application/json", "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });
    if (response.status === 401 || response.status === 400) {
      setSession(null);
      return false;
    }
    if (!response.ok) {
      return false;
    }
    setSession((await response.json()) as AdminSession);
    return true;
  } catch {
    return false;
  }
}

function tokenExpiring(session: AdminSession): boolean {
  const expires = Date.parse(session.expiresAt);
  return !Number.isNaN(expires) && expires - Date.now() < renewLeadMs;
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

export async function apiBlob(path: string): Promise<Blob | null> {
  const session = getSession();
  const headers = new Headers({ Accept: "image/jpeg, image/png, image/webp" });
  if (session) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  const response = await fetch(`${root}${path}`, { headers });
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new ApiError(await problemDetail(response), response.status);
  }
  return response.blob();
}

export function uploadProfilePhoto(file: File, onProgress: (ratio: number) => void): Promise<AdminUser> {
  return sendProfilePhoto("POST", file, onProgress);
}

export function removeProfilePhoto(): Promise<AdminUser> {
  return sendProfilePhoto("DELETE");
}

function sendProfilePhoto(method: "POST" | "DELETE", file?: File, onProgress?: (ratio: number) => void): Promise<AdminUser> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(method, `${root}/users/me/photo`);
    const session = getSession();
    if (session) {
      xhr.setRequestHeader("Authorization", `Bearer ${session.accessToken}`);
    }
    xhr.setRequestHeader("Accept", "application/json");
    if (onProgress) {
      xhr.upload.onprogress = (event) => {
        if (event.lengthComputable) {
          onProgress(event.loaded / event.total);
        }
      };
    }
    xhr.onload = () => {
      const body = parseBody(xhr.responseText);
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(body as AdminUser);
        return;
      }
      reject(new ApiError(body?.detail ?? body?.title ?? "The profile picture could not be saved.", xhr.status));
    };
    xhr.onerror = () => reject(new ApiError("The profile picture could not be saved.", 0));
    if (file) {
      const body = new FormData();
      body.append("file", file);
      xhr.send(body);
    } else {
      xhr.send();
    }
  });
}

function parseBody(raw: string): { detail?: string; title?: string } | null {
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw) as { detail?: string; title?: string };
  } catch {
    return null;
  }
}
