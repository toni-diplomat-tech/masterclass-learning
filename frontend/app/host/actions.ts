"use server";

import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";
import { backendUrl } from "@/lib/backend";
import { HOST_COOKIE, HOST_LOGIN_PATH } from "@/lib/host-cookie";
import { readHostCookie } from "@/lib/host-session";

export type LoginState = { error?: string };

const BACKEND_TIMEOUT_MS = 10000; // Hashing the password takes a moment on purpose.

export async function login(_previous: LoginState, formData: FormData): Promise<LoginState> {
  const password = String(formData.get("password") ?? "");

  let response: Response;
  try {
    response = await fetch(`${backendUrl()}/api/host/login`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ password }),
      cache: "no-store",
      signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS),
    });
  } catch {
    return { error: "The server can't be reached. Try again." };
  }
  if (response.status === 401) return { error: "Wrong password." };

  const session = response.ok ? readHostCookie(response.headers.getSetCookie()) : null;
  if (!session) return { error: `Login failed (HTTP ${response.status}). Try again.` };

  // Behind Caddy the browser talks HTTPS, so the cookie can be Secure; plain HTTP locally.
  const https = (await headers()).get("x-forwarded-proto") === "https";
  (await cookies()).set(HOST_COOKIE, session.value, {
    httpOnly: true,
    sameSite: "lax",
    secure: https,
    path: "/",
    expires: session.expires,
  });
  redirect("/host");
}

export async function logout(): Promise<void> {
  const cookieStore = await cookies();
  const session = cookieStore.get(HOST_COOKIE)?.value;
  if (session) {
    // The session lives in the cookie; the backend call just mirrors the logout there.
    await fetch(`${backendUrl()}/api/host/logout`, {
      method: "POST",
      headers: { cookie: `${HOST_COOKIE}=${session}` },
      cache: "no-store",
      signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS),
    }).catch(() => undefined);
  }
  cookieStore.delete(HOST_COOKIE);
  redirect(HOST_LOGIN_PATH);
}
