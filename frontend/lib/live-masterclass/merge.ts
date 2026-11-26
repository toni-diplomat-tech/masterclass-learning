import type { CurrentState, OwnAnswer } from "./hub";
import type { AnswerCount, AnswerOutcome, AnswerResult, LiveSession, MyResult, QuestionRevealed, SessionLeaderboard } from "./types";

// Pure rules for merging what arrives (broadcasts, GetCurrentState replies) into what is shown, since
// they can overtake each other: the shown Session never moves backwards and answer counts only grow.

/** This phone's Answer, with the Question it answers and, once known from the server, its outcome. */
export type Answer = {
  questionId: number;
  option: number | null;
  status: "sending" | "locked" | "refused";
  result: AnswerResult | null;
  outcome: AnswerOutcome | null;
};

/** The shown Session with a newer answer count; counts only grow, so one that overtook a later one is dropped. */
export function withCount(session: LiveSession, count: AnswerCount): LiveSession {
  return session.step === "question" &&
    session.question.questionId === count.questionId &&
    (session.count?.answered ?? 0) < count.answered
    ? { ...session, count }
    : session;
}

/** The LiveSession a state shows. */
function liveSessionOf(state: CurrentState): LiveSession {
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

/** The Session the screen shows, if any. */
export function shownSessionId(session: LiveSession): number | null {
  switch (session.step) {
    case "question":
      return session.question.sessionId;
    case "revealed":
      return session.revealed.sessionId;
    case "ended":
      return session.ended.sessionId;
    default:
      return null;
  }
}

/** The Question the screen shows (running or revealed), if any. */
export function shownQuestionId(session: LiveSession): number | null {
  if (session.step === "question") return session.question.questionId;
  if (session.step === "revealed") return session.revealed.questionId;
  return null;
}

/** This phone's Answer as the server has it (e.g. after a reload), unless the phone knows more already. */
export function restoredAnswer(mine: Answer | null, own: OwnAnswer): Answer | null {
  if (own.option !== null) {
    return { questionId: own.questionId, option: own.option, status: "locked", result: "Accepted", outcome: own.outcome };
  }
  if (!own.canAnswer) {
    return { questionId: own.questionId, option: null, status: "refused", result: "JoinedAfterStart", outcome: own.outcome };
  }
  return mine?.questionId === own.questionId ? mine : null;
}

/**
 * This phone's result on a revealed Question, given its Answer to that Question. The server's outcome
 * when it gave one (restored after a reconnect); for an Answer locked in live, the reveal's correct option
 * decides; with no locked Answer, it Timed out.
 */
export function resultOf(revealed: QuestionRevealed, answer: Answer | null): MyResult {
  if (answer?.result === "JoinedAfterStart") return "JoinedAfterStart";
  if (answer?.result === "AlreadyAnswered") return "AlreadyAnswered";
  if (answer?.status !== "locked") return "TimedOut";
  if (answer.outcome) return answer.outcome;
  return answer.option === revealed.correctOption ? "Correct" : "Incorrect";
}
