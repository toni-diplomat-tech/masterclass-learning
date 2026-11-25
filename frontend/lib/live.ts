import type { HubConnection } from "@microsoft/signalr";

// Browser-side: the live meeting's messages on the Quiz hub (backend Live/QuizHub.cs).

export type Difficulty = "Easy" | "Medium" | "Hard";

export type DifficultyMix = { easy: number; medium: number; hard: number };

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

/** Host only: starts a Workshop's Session. Resolves to every reason it was refused (empty: it started). */
export async function startSession(
  connection: HubConnection,
  masterclassId: number,
  workshopId: number,
  mix: DifficultyMix,
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
