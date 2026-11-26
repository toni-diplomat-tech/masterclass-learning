import { cookies } from "next/headers";
import { backendUrl } from "./backend";
import type { MasterclassStatus } from "./domain";
import { PARTICIPANT_COOKIE } from "./participant";

// Server-side only: Recaps, readable by anyone with the link (no login, no expiry).

export type RecapOutcome = "PerfectScore" | "MissedQuestions" | "NotAttended";

/** A Question answered Incorrect (answer = the chosen option's text) or Timed out (answer null). */
export type MissedQuestion = { text: string; answer: string | null; timedOut: boolean; correctOption: string };

export type RecapLesson = { number: number; title: string; url: string; questions: MissedQuestion[] };

/** Lessons: [] for a Perfect score, the missed Questions' Lessons, or every Lesson when Not attended. */
export type RecapWorkshop = { number: number; title: string; outcome: RecapOutcome; lessons: RecapLesson[] };

/** Only Workshops whose Session has ended, in Course order. */
export type Recap = {
  nickname: string;
  masterclassId: number;
  masterclassName: string;
  masterclassStatus: MasterclassStatus;
  courseTitle: string;
  workshops: RecapWorkshop[];
};

const BACKEND_TIMEOUT_MS = 5000;

/** The Recap behind this token, or null if there is none. */
export async function getRecap(recapToken: string): Promise<Recap | null> {
  const response = await fetch(`${backendUrl()}/api/recaps/${encodeURIComponent(recapToken)}`, {
    cache: "no-store",
    signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS),
  });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Loading the Recap failed: HTTP ${response.status}`);
  return response.json();
}

/** The token of this browser's Participant's Recap link (/recap/<token>), or null without a known cookie. */
export async function currentRecapToken(): Promise<string | null> {
  const token = (await cookies()).get(PARTICIPANT_COOKIE)?.value;
  if (!token) return null;
  const response = await fetch(`${backendUrl()}/api/participants/me/recap`, {
    headers: { cookie: `${PARTICIPANT_COOKIE}=${token}` },
    cache: "no-store",
    signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS),
  });
  if (response.status === 401) return null;
  if (!response.ok) throw new Error(`Loading the Recap link failed: HTTP ${response.status}`);
  return ((await response.json()) as { recapToken: string }).recapToken;
}

/** How a missed Lesson is named, e.g. "Unit tests · 05 Writing your first test". */
export function lessonLabel(workshopTitle: string, lesson: { number: number; title: string }): string {
  return `${workshopTitle} · ${String(lesson.number).padStart(2, "0")} ${lesson.title}`;
}
