import type { HubConnection } from "@microsoft/signalr";
import type { LeaderboardEntry, SessionLeaderboard } from "@/lib/leaderboard";
import type { MasterclassStatus } from "@/lib/masterclasses";
import type { AnswerCount, LiveSession, QuestionRevealed, QuestionStarted, SessionEnded } from "@/lib/live";

// Browser-side: GetCurrentState, called on every connect and reconnect (SignalR doesn't replay missed
// messages), and the projector's Resume. Backend: Live/QuizHub.Recovery.cs, Quiz/CurrentState.cs.

export type AnswerOutcome = "Correct" | "Incorrect" | "TimedOut";

/**
 * The asking Participant's Answer to the Question shown: `canAnswer` false when they joined after it
 * started; `option` they chose (null: not answered or Timed out); `outcome` only once revealed.
 */
export type OwnAnswer = { questionId: number; canAnswer: boolean; option: number | null; outcome: AnswerOutcome | null };

/**
 * What to show now. `masterclassStatus` is "Closed" once the Host closed the Masterclass, so a screen
 * that missed the MasterclassClosed broadcast still moves on to its end. Only the part named by `step` is set; `leaderboard` (the Session leaderboard) with
 * Revealed and Ended; `participant` only for a Participant's phone, with their own leaderboard entry
 * (`standing`, null before they scored on a revealed Question).
 */
export type CurrentState = {
  masterclassId: number;
  masterclassStatus: MasterclassStatus;
  step: "Waiting" | "Question" | "Revealed" | "Ended";
  question: QuestionStarted | null;
  count: AnswerCount | null;
  revealed: QuestionRevealed | null;
  ended: SessionEnded | null;
  participant: { participantId: number; nickname: string; answer: OwnAnswer | null; standing: LeaderboardEntry | null } | null;
  leaderboard: SessionLeaderboard | null;
};

/** Follows the Masterclass's broadcasts and returns what to show now; null if refused (not theirs). */
export function getCurrentState(connection: HubConnection, masterclassId: number): Promise<CurrentState | null> {
  return connection.invoke<CurrentState | null>("GetCurrentState", masterclassId);
}

/** Host only (Resume button): the backend timer follows the running Session again. Returns the state now. */
export function resumeSession(connection: HubConnection, masterclassId: number): Promise<CurrentState | null> {
  return connection.invoke<CurrentState | null>("ResumeSession", masterclassId);
}

/** The LiveSession a state shows. */
export function liveSessionOf(state: CurrentState): LiveSession {
  switch (state.step) {
    case "Question":
      return { step: "question", question: state.question!, count: state.count };
    case "Revealed":
      return { step: "revealed", revealed: state.revealed! };
    case "Ended":
      return { step: "ended", ended: state.ended! };
    default:
      return { step: "waiting" };
  }
}

/**
 * The state's LiveSession, unless a broadcast that arrived while the state was on its way already moved
 * the screen further on (then the shown one stays). A running Question keeps the higher answer count.
 */
export function newerSession(shown: LiveSession, state: CurrentState): LiveSession {
  const fromState = liveSessionOf(state);
  const order = compare(progress(fromState), progress(shown));
  if (order < 0) return shown;
  if (order === 0 && shown.step === "question" && fromState.step === "question") {
    return (shown.count?.answered ?? 0) > (fromState.count?.answered ?? 0) ? shown : fromState;
  }
  return fromState;
}

// How far a Masterclass's meeting has got: Session, Question number, step within the Question.
function progress(session: LiveSession): number[] {
  switch (session.step) {
    case "waiting":
      return [-1];
    case "question":
      return [session.question.sessionId, session.question.number, 0];
    case "revealed":
      return [session.revealed.sessionId, session.revealed.number, 1];
    case "ended":
      return [session.ended.sessionId, Number.MAX_SAFE_INTEGER, 2];
  }
}

function compare(a: number[], b: number[]): number {
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    const difference = (a[i] ?? -1) - (b[i] ?? -1);
    if (difference !== 0) return difference;
  }
  return 0;
}

/**
 * The state's leaderboard, unless the shown one is at least as recent: from a later Session, or the
 * final one of the same Session. Keeps the shown one when the state has none.
 */
export function newerLeaderboard(shown: SessionLeaderboard | null, fromState: SessionLeaderboard | null): SessionLeaderboard | null {
  if (!fromState) return shown;
  if (!shown || shown.sessionId < fromState.sessionId) return fromState;
  if (shown.sessionId > fromState.sessionId || (shown.ended && !fromState.ended)) return shown;
  return fromState;
}
