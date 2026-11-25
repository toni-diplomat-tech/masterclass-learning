import type { HubConnection } from "@microsoft/signalr";

// Browser-side: the live meeting's messages on the Quiz hub (backend Live/QuizHub.cs).

export type Difficulty = "Easy" | "Medium" | "Hard";

/** A number per Difficulty: a Workshop's Question counts, or the mix the Host sets for a Session. */
export type DifficultyCounts = { easy: number; medium: number; hard: number };

/** Server → clients when a Question starts. Never says which option is correct. Times are UTC ISO strings. */
export type QuestionStarted = {
  masterclassId: number;
  sessionId: number;
  workshopTitle: string;
  questionId: number;
  number: number;
  questionCount: number;
  difficulty: Difficulty;
  text: string;
  options: { position: number; text: string }[];
  startedAt: string;
  deadline: string;
};

export const QUESTION_STARTED = "QuestionStarted";

/** Server → clients after each accepted Answer: how many of those who can answer the Question have. */
export type AnswerCount = {
  masterclassId: number;
  sessionId: number;
  questionId: number;
  answered: number;
  participants: number;
};

export const ANSWER_COUNT_UPDATED = "AnswerCountUpdated";

/** Server → clients when the running Question is revealed: the correct option and the counts. */
export type QuestionRevealed = {
  masterclassId: number;
  sessionId: number;
  workshopTitle: string;
  questionId: number;
  number: number;
  questionCount: number;
  difficulty: Difficulty;
  text: string;
  correctOption: number;
  options: { position: number; text: string; answers: number }[];
  answered: number;
  timedOut: number;
  revealedAt: string;
  /** No Question follows: the Session ends after the reveal pause. */
  lastQuestion: boolean;
};

export const QUESTION_REVEALED = "QuestionRevealed";

/** Server → clients after the pause following the Session's last reveal. */
export type SessionEnded = { masterclassId: number; sessionId: number; endedAt: string };

export const SESSION_ENDED = "SessionEnded";

/** What the server made of an Answer; only "Accepted" records it. */
export type AnswerResult =
  | "Accepted"
  | "TooLate"
  | "AlreadyAnswered"
  | "JoinedAfterStart"
  | "UnknownOption"
  | "NoSuchQuestion"
  | "UnknownParticipant";

/** Participant only (by cookie): answers a running Question with the option at `option`. */
export async function submitAnswer(connection: HubConnection, questionId: number, option: number): Promise<AnswerResult> {
  const reply = await connection.invoke<{ result: AnswerResult }>("SubmitAnswer", questionId, option);
  return reply.result;
}

/** Where the live Session stands, as the server last said. */
export type LiveSession =
  | { step: "waiting" }
  | { step: "question"; question: QuestionStarted; count: AnswerCount | null }
  | { step: "revealed"; revealed: QuestionRevealed }
  | { step: "ended"; ended: SessionEnded };

/**
 * Keeps `setSession` up to date with a Masterclass's Session broadcasts: QuestionStarted,
 * AnswerCountUpdated, QuestionRevealed and SessionEnded. Register before starting the connection.
 */
export function followSession(
  connection: HubConnection,
  masterclassId: number,
  setSession: (update: (session: LiveSession) => LiveSession) => void,
): void {
  connection.on(QUESTION_STARTED, (question: QuestionStarted) => {
    if (question.masterclassId === masterclassId) setSession(() => ({ step: "question", question, count: null }));
  });
  connection.on(ANSWER_COUNT_UPDATED, (count: AnswerCount) => {
    if (count.masterclassId !== masterclassId) return;
    // Counts only grow; one that overtook a later one must not lower it.
    setSession((session) =>
      session.step === "question" &&
      session.question.questionId === count.questionId &&
      (session.count?.answered ?? 0) < count.answered
        ? { ...session, count }
        : session,
    );
  });
  connection.on(QUESTION_REVEALED, (revealed: QuestionRevealed) => {
    if (revealed.masterclassId === masterclassId) setSession(() => ({ step: "revealed", revealed }));
  });
  connection.on(SESSION_ENDED, (ended: SessionEnded) => {
    if (ended.masterclassId === masterclassId) setSession(() => ({ step: "ended", ended }));
  });
}

/** Host only: starts a Workshop's Session. Resolves to every reason it was refused (empty: it started). */
export async function startSession(
  connection: HubConnection,
  masterclassId: number,
  workshopId: number,
  mix: DifficultyCounts,
): Promise<string[]> {
  const reply = await connection.invoke<{ problems: string[] }>("StartSession", masterclassId, workshopId, mix);
  return reply.problems;
}

/**
 * Starts the connection and runs `follow` (the watch call that joins the Masterclass's broadcasts)
 * after the first start and after every reconnect. Retries the first start, which automatic
 * reconnects don't cover. Returns a function that stops it all.
 */
export function keepFollowing(
  connection: HubConnection,
  follow: () => Promise<void>,
  setConnected: (connected: boolean) => void,
): () => void {
  let stopped = false;
  let retry: ReturnType<typeof setTimeout> | undefined;
  const resync = () =>
    follow()
      .then(() => setConnected(true))
      .catch(() => undefined);

  connection.onreconnecting(() => setConnected(false));
  connection.onreconnected(() => void resync());
  connection.onclose(() => setConnected(false));

  const start = () =>
    connection
      .start()
      .then(() => (stopped ? undefined : resync()))
      .catch(() => {
        setConnected(false);
        if (!stopped) retry = setTimeout(start, 5000);
      });
  void start();

  return () => {
    stopped = true;
    clearTimeout(retry);
    void connection.stop();
  };
}
