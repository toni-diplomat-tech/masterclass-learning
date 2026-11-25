import type { MasterclassStatus } from "./masterclasses";
import { hostFetch } from "./host-session";

// Server-side only: the Host's results of a Masterclass. They hold every Participant's Recap token, so
// they come only through hostFetch.

export type QuestionResult = {
  number: number;
  difficulty: "Easy" | "Medium" | "Hard";
  text: string;
  correct: number;
  incorrect: number;
  timedOut: number;
  /** Rounded to a whole percent; null when nobody was there to answer. */
  percentCorrect: number | null;
};

/** A Session's revealed Questions in the order they ran; ended: the results are final. */
export type SessionResults = {
  sessionId: number;
  workshopNumber: number;
  workshopTitle: string;
  ended: boolean;
  questions: QuestionResult[];
};

/** A Participant's points over every revealed Question, and the token of their Recap link. */
export type ParticipantResult = {
  participantId: number;
  nickname: string;
  points: number;
  recapToken: string;
};

/** Sessions in Course order; Participants by Nickname. */
export type MasterclassResults = {
  masterclassId: number;
  masterclassName: string;
  masterclassStatus: MasterclassStatus;
  courseTitle: string;
  sessions: SessionResults[];
  participants: ParticipantResult[];
};

/** The Masterclass's results, Open or Closed, or null if there is none with this id. */
export async function getMasterclassResults(id: number): Promise<MasterclassResults | null> {
  const response = await hostFetch(`/api/host/masterclasses/${id}/results`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Loading the results failed: HTTP ${response.status}`);
  return response.json();
}
