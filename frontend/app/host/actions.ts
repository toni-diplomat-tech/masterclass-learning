"use server";

import { refresh } from "next/cache";
import { cookies, headers } from "next/headers";
import { redirect, unstable_rethrow } from "next/navigation";
import { backendUrl } from "@/lib/backend";
import { importCourse, type ImportSummary } from "@/lib/courses";
import { closeMasterclass, createMasterclass, openMasterclass } from "@/lib/masterclasses";
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

export type ImportState = { summary?: ImportSummary; problems?: string[]; error?: string };

export async function importCourseFile(_previous: ImportState, formData: FormData): Promise<ImportState> {
  const file = formData.get("file");
  if (!(file instanceof File) || file.size === 0) return { error: "Choose an import file to upload." };

  try {
    const result = await importCourse(await file.text());
    if ("problems" in result) return { problems: result.problems };
    refresh();
    return { summary: result.summary };
  } catch (error) {
    unstable_rethrow(error); // hostFetch redirects to the login page when the session has ended.
    return { error: "The import failed. Try again." };
  }
}

export type CreateMasterclassState = { problems?: string[]; error?: string };

export async function createMasterclassAction(
  _previous: CreateMasterclassState,
  formData: FormData,
): Promise<CreateMasterclassState> {
  const courseId = Number(formData.get("courseId"));
  const name = String(formData.get("name") ?? "");

  try {
    const result = await createMasterclass(courseId, name);
    if ("problems" in result) return { problems: result.problems };
    refresh();
    return {};
  } catch (error) {
    unstable_rethrow(error); // hostFetch redirects to the login page when the session has ended.
    return { error: "Creating the Masterclass failed. Try again." };
  }
}

export type CloseMasterclassState = { problems?: string[]; error?: string };

/** Closes the Masterclass from its projector view, which then shows the Masterclass leaderboard. */
export async function closeMasterclassAction(masterclassId: number): Promise<CloseMasterclassState> {
  try {
    const { problems } = await closeMasterclass(masterclassId);
    if (problems.length > 0) return { problems };
    refresh();
    return {};
  } catch (error) {
    unstable_rethrow(error); // hostFetch redirects to the login page when the session has ended.
    return { error: "Closing the Masterclass failed. Try again." };
  }
}

/** Opens a Draft Masterclass from its projector view, which then shows the Join code. */
export async function openMasterclassAction(masterclassId: number): Promise<void> {
  await openMasterclass(masterclassId);
  refresh();
}
