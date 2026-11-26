"use server";

import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";
import { textField } from "@/lib/form-data";
import { joinMasterclass, PARTICIPANT_COOKIE } from "@/lib/participant";

export type JoinState = { error?: string; joinCode?: string };

export async function join(_previous: JoinState, formData: FormData): Promise<JoinState> {
  const joinCode = textField(formData, "joinCode").replace(/\s/g, "");
  if (!/^[0-9]{6}$/.test(joinCode)) return { joinCode, error: "A Join code has 6 digits." };

  let result;
  try {
    result = await joinMasterclass(joinCode);
  } catch {
    return { joinCode, error: "The server can't be reached. Try again." };
  }
  if (result.kind === "refused") return { joinCode, error: "No open Masterclass has this Join code." };

  // Behind Caddy the browser talks HTTPS, so the cookie can be Secure; plain HTTP locally.
  const https = (await headers()).get("x-forwarded-proto") === "https";
  (await cookies()).set(PARTICIPANT_COOKIE, result.cookie.value, {
    httpOnly: true,
    sameSite: "lax",
    secure: https,
    path: "/",
    expires: result.cookie.expires,
  });
  redirect("/play");
}
