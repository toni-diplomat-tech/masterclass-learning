import { backendUrl } from "./backend";
import type { MasterclassStatus } from "./domain";

// Who a Participant cookie belongs to, and where that phone belongs. Kept apart from participant.ts
// so the proxy can import it without pulling in next/headers.

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

export const BACKEND_TIMEOUT_MS = 5000;

/** The Participant this cookie token belongs to, or null if the backend doesn't know it. */
export async function participantByToken(token: string): Promise<Participant | null> {
  const response = await fetch(`${backendUrl()}/api/participants/me`, {
    headers: { cookie: `${PARTICIPANT_COOKIE}=${token}` },
    cache: "no-store",
    signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS),
  });
  if (response.status === 401) return null;
  if (!response.ok) throw new Error(`Loading the Participant failed: HTTP ${response.status}`);
  return response.json();
}

/**
 * Whether this Participant follows a live Masterclass on /play: their Masterclass is still
 * Masterclass Open. Once it is Masterclass Closed, /play shows that it ended and `/` lets them join
 * another one.
 */
export function inMasterclassOpen(participant: Participant): boolean {
  return participant.masterclassStatus === "Open";
}
