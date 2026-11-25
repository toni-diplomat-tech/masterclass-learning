import { cookies } from "next/headers";
import { backendUrl } from "./backend";
import type { MasterclassStatus } from "./masterclasses";
import { readSetCookie } from "./set-cookie";

// Server-side only: joining a Masterclass and recognizing the Participant by their cookie.

/** The backend's Participant cookie (ParticipantEndpoints.CookieName): HTTP-only, holds a random token. */
export const PARTICIPANT_COOKIE = "mq_participant";

export type Participant = {
  id: number;
  masterclassId: number;
  masterclassName: string;
  masterclassStatus: MasterclassStatus;
  nickname: string;
  joinedAt: string;
};

export type JoinResult =
  | { kind: "joined"; participant: Participant; cookie: { value: string; expires?: Date } }
  | { kind: "refused" };

const BACKEND_TIMEOUT_MS = 5000;

async function participantCookieHeader(): Promise<Record<string, string>> {
  const token = (await cookies()).get(PARTICIPANT_COOKIE)?.value;
  return token ? { cookie: `${PARTICIPANT_COOKIE}=${token}` } : {};
}

/**
 * Joins the Open Masterclass with this Join code. The browser's current cookie goes along, so a
 * Participant who joins again gets the same Nickname back.
 */
export async function joinMasterclass(joinCode: string): Promise<JoinResult> {
  const response = await fetch(`${backendUrl()}/api/participants/join`, {
    method: "POST",
    headers: { "content-type": "application/json", ...(await participantCookieHeader()) },
    body: JSON.stringify({ joinCode }),
    cache: "no-store",
    signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS),
  });
  if (response.status === 404) return { kind: "refused" };
  const cookie = response.ok ? readSetCookie(response.headers.getSetCookie(), PARTICIPANT_COOKIE) : null;
  if (!cookie) throw new Error(`Joining failed: HTTP ${response.status}`);
  return { kind: "joined", participant: await response.json(), cookie };
}

/** The Participant this browser's cookie belongs to, or null if it has none (or an unknown one). */
export async function currentParticipant(): Promise<Participant | null> {
  const cookie = await participantCookieHeader();
  if (!cookie.cookie) return null;
  const response = await fetch(`${backendUrl()}/api/participants/me`, {
    headers: cookie,
    cache: "no-store",
    signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS),
  });
  if (response.status === 401) return null;
  if (!response.ok) throw new Error(`Loading the Participant failed: HTTP ${response.status}`);
  return response.json();
}
