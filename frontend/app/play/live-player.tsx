"use client";

import { useEffect, useState } from "react";
import { LiveQuestion } from "@/app/live-question";
import { keepFollowing, QUESTION_STARTED, type QuestionStarted } from "@/lib/live";
import { connectToQuizHub } from "@/lib/quiz-hub";

/**
 * The phone follows the server: the waiting screen until a Question starts, then the Question with
 * its countdown. The hub knows the Participant by their HTTP-only cookie (same origin).
 */
export function LivePlayer({ masterclassId }: { masterclassId: number }) {
  const [question, setQuestion] = useState<QuestionStarted | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    const connection = connectToQuizHub();
    connection.on(QUESTION_STARTED, (started: QuestionStarted) => {
      if (started.masterclassId === masterclassId) setQuestion(started);
    });
    return keepFollowing(
      connection,
      async () => {
        await connection.invoke("WatchAsParticipant");
      },
      setConnected,
    );
  }, [masterclassId]);

  return (
    <>
      {question ? (
        <LiveQuestion question={question} />
      ) : (
        <p data-testid="waiting">Waiting for the Host to start the next Session…</p>
      )}
      {!connected && <small style={{ display: "block" }}>Connecting to live updates…</small>}
    </>
  );
}
