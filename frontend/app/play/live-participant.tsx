"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { LiveQuestion } from "@/app/live-question";
import { RevealedQuestion } from "@/app/revealed-question";
import { useLivePhone, type AnswerOutcome, type AnswerResult, type LiveSession, type MyAnswer, type MyResult } from "@/lib/live-masterclass";
import { MyScore } from "./my-score";
import { RecapLink } from "./recap-link";

const REFUSALS: Partial<Record<AnswerResult, string>> = {
  TooLate: "Too late: time ran out before your Answer arrived.",
  JoinedAfterStart: "You joined during this Question. The next one is yours.",
  AlreadyAnswered: "You already answered this Question.",
};

/**
 * The phone follows the server: Waiting → Question → Answer locked → Revealed → … → Session over.
 * There is nowhere else to go. The hub knows the Participant by their HTTP-only cookie (same origin).
 */
export function LiveParticipant({ masterclassId, recapToken }: { masterclassId: number; recapToken: string | null }) {
  const { session, leaderboard, connected, closed, participantId, myAnswer, myResult, answer } = useLivePhone(masterclassId);
  const router = useRouter();

  // Closed: the page renders again as "ended", with the Recap link.
  useEffect(() => {
    if (closed) router.refresh();
  }, [closed, router]);

  return (
    <>
      <Screen session={session} myAnswer={myAnswer} myResult={myResult} onChoose={answer} />
      {session.step === "revealed" && <MyScore leaderboard={leaderboard} participantId={participantId} />}
      {session.step === "ended" && <MyScore leaderboard={leaderboard} participantId={participantId} sessionOver />}
      {session.step === "ended" && <RecapLink recapToken={recapToken} />}
      {!connected && <small style={{ display: "block" }}>Connecting to live updates…</small>}
    </>
  );
}

function Screen({
  session,
  myAnswer,
  myResult,
  onChoose,
}: {
  session: LiveSession;
  myAnswer: MyAnswer | null;
  myResult: MyResult | null;
  onChoose: (questionId: number, position: number) => Promise<void>;
}) {
  switch (session.step) {
    case "waiting":
      return <p data-testid="waiting">Waiting for the Host to start the next Session…</p>;
    case "question": {
      const { question } = session;
      return (
        <LiveQuestion
          question={question}
          chosen={myAnswer?.option ?? null}
          locked={myAnswer !== null}
          onChoose={(position) => void onChoose(question.questionId, position)}
          footer={<AnswerStatus answer={myAnswer} />}
        />
      );
    }
    case "revealed":
      return (
        <RevealedQuestion revealed={session.revealed}>
          <MyResultLine result={myResult} />
        </RevealedQuestion>
      );
    case "ended":
      return (
        <section data-testid="session-over">
          <h2>Session over</h2>
          <p>Waiting for the Host to start the next Session…</p>
        </section>
      );
  }
}

function AnswerStatus({ answer }: { answer: MyAnswer | null }) {
  if (!answer) return null;
  if (answer.status === "sending") return <p data-testid="answer-status">Sending your Answer…</p>;
  if (answer.status === "locked")
    return (
      <p data-testid="answer-locked" style={{ fontSize: "1.5rem", fontWeight: "bold" }}>
        Answer locked
      </p>
    );
  return <p data-testid="answer-status">{(answer.result && REFUSALS[answer.result]) ?? "Your Answer was not accepted."}</p>;
}

const OUTCOMES: Record<AnswerOutcome, string> = { Correct: "Correct", Incorrect: "Incorrect", TimedOut: "Timed out" };

function MyResultLine({ result }: { result: MyResult | null }) {
  if (result === "JoinedAfterStart") {
    return <p data-testid="my-result">You joined during this Question.</p>;
  }
  if (result === "AlreadyAnswered") {
    // Answered earlier (e.g. before a reload): this tap isn't necessarily the option that counts.
    return <p data-testid="my-result">Your earlier Answer counts.</p>;
  }
  return (
    <p data-testid="my-result" style={{ fontSize: "2rem", fontWeight: "bold" }}>
      {OUTCOMES[result ?? "TimedOut"]}
    </p>
  );
}
