import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { backendUrl } from "./backend";
import { HOST_COOKIE, HOST_LOGIN_PATH } from "./host-cookie";
import { readSetCookie } from "./set-cookie";

// Server-side only. Every Host page and Server Action reaches the backend through hostFetch, so the
// backend, not the frontend, decides whether the session is valid.

const BACKEND_TIMEOUT_MS = 5000;

/**
 * Calls a Host endpoint (`/api/host/...`) with the browser's Host session.
 * Without a session, or when the backend answers 401, redirects to the login page.
 */
export async function hostFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const session = (await cookies()).get(HOST_COOKIE)?.value;
  if (!session) redirect(HOST_LOGIN_PATH);

  const headers = new Headers(init.headers);
  headers.set("cookie", `${HOST_COOKIE}=${session}`);
  const response = await fetch(`${backendUrl()}${path}`, {
    cache: "no-store",
    signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS),
    ...init,
    headers,
  });
  if (response.status === 401) redirect(HOST_LOGIN_PATH);
  return response;
}

/** Redirects to the login page unless the browser has a valid Host session. */
export async function requireHost(): Promise<void> {
  const response = await hostFetch("/api/host/session");
  if (!response.ok) throw new Error(`Host session check failed: HTTP ${response.status}`);
}

/** The Host session cookie from the backend's login response, to relay to the browser. */
export function readHostCookie(setCookies: string[]): { value: string; expires?: Date } | null {
  return readSetCookie(setCookies, HOST_COOKIE);
}
