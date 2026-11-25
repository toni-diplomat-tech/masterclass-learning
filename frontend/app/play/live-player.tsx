"use client";

import { useEffect, useRef, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { useRouter } from "next/navigation";
import { LiveQuestion } from "@/app/live-question";
import { RevealedQuestion } from "@/app/revealed-question";
import { getCurrentState, newerLeaderboard, newerSession, type OwnAnswer } from "@/lib/current-state";
import { followSession, keepFollowing, submitAnswer, type AnswerResult, type LiveSession, type QuestionRevealed } from "@/lib/live";
import { connectToQuizHub } from "@/lib/quiz-hub";
import { followLeaderboard, leaderboardFor, type SessionLeaderboard } from "@/lib/leaderboard";
import { MyScore } from "./my-score";
import { RecapLink } from "./recap-link";

/** This phone's tap on a Question: being sent, locked in by the server, or refused (with the reason). */
type MyAnswer = { questionId: number; position: number; status: "sending" | "locked" | "refused"; result?: AnswerResult };

const REFUSALS: Partial<Record<AnswerResult, string>> = {
  TooLate: "Too late: time ran out before your Answer arrived.",
  JoinedAfterStart: "You joined during this Question. The next one is yours.",
  AlreadyAnswered: "You already answered this Question.",
};

/**
 * The phone follows the server: Waiting → Question → Answer locked → Revealed → … → Session over.
 * There is nowhere else to go. The hub knows the Participant by their HTTP-only cookie (same origin).
 */
export function LivePlayer({ masterclassId, recapToken }: { masterclassId: number; recapToken: string | null }) {
  const [session, setSession] = useState<LiveSession>({ step: "waiting" });
  const [answer, setAnswer] = useState<MyAnswer | null>(null);
  const [connected, setConnected] = useState(false);
  const [leaderboard, setLeaderboard] = useState<SessionLeaderboard | null>(null);
  const [participantId, setParticipantId] = useState<number | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);
  const router = useRouter();

  useEffect(() => {
    const connection = connectToQuizHub();
    connectionRef.current = connection;
    followSession(connection, masterclassId, setSession);
    followLeaderboard(connection, masterclassId, setLeaderboard);
    // Closed: the page renders again as "ended", with the Recap link.
    connection.on("MasterclassClosed", (closed: { masterclassId: number }) => {
      if (closed.masterclassId === masterclassId) router.refresh();
    });
    const stop = keepFollowing(
      connection,
      async () => {
        // On every (re)connect: the server says where the Session is and what this phone answered.
        const state = await getCurrentState(connection, masterclassId);
        if (!state) return;
        setParticipantId(state.participant?.participantId ?? null);
        setLeaderboard((shown) => newerLeaderboard(shown, state.leaderboard));
        setSession((shown) => newerSession(shown, state));
        const own = state.participant?.answer;
        if (own) setAnswer((mine) => restoredAnswer(mine, own));
      },
      setConnected,
    );
    return () => {
      connectionRef.current = null;
      stop();
    };
  }, [masterclassId, router]);

  const choose = async (questionId: number, position: number) => {
    const connection = connectionRef.current;
    if (!connection) return;
    setAnswer({ questionId, position, status: "sending" });
    try {
      const result = await submitAnswer(connection, questionId, position);
      setAnswer({ questionId, position, status: result === "Accepted" || result === "AlreadyAnswered" ? "locked" : "refused", result });
    } catch {
      // Not delivered (e.g. reconnecting): let them tap again.
      setAnswer(null);
    }
  };

  return (
    <>
      <Screen session={session} answer={answer} onChoose={choose} />
      {session.step === "revealed" && (
        <MyScore leaderboard={leaderboardFor(leaderboard, session.revealed.sessionId)} participantId={participantId} />
      )}
      {session.step === "ended" && (
        <MyScore leaderboard={leaderboardFor(leaderboard, session.ended.sessionId)} participantId={participantId} sessionOver />
      )}
      {session.step === "ended" && <RecapLink recapToken={recapToken} />}
      {!connected && <small style={{ display: "block" }}>Connecting to live updates…</small>}
    </>
  );
}

// This phone's Answer as the server has it (e.g. after a reload), unless the phone knows more already.
function restoredAnswer(mine: MyAnswer | null, own: OwnAnswer): MyAnswer | null {
  if (own.option !== null) return { questionId: own.questionId, position: own.option, status: "locked", result: "Accepted" };
  if (!own.canAnswer) return { questionId: own.questionId, position: 0, status: "refused", result: "JoinedAfterStart" };
  return mine?.questionId === own.questionId ? mine : null;
}

function Screen({
  session,
  answer,
  onChoose,
}: {
  session: LiveSession;
  answer: MyAnswer | null;
  onChoose: (questionId: number, position: number) => void;
}) {
  switch (session.step) {
    case "waiting":
      return <p data-testid="waiting">Waiting for the Host to start the next Session…</p>;
    case "question": {
      const { question } = session;
      const mine = answer?.questionId === question.questionId ? answer : null;
      return (
        <LiveQuestion
          question={question}
          chosen={mine?.position ?? null}
          onChoose={(position) => onChoose(question.questionId, position)}
          footer={<AnswerStatus answer={mine} />}
        />
      );
    }
    case "revealed": {
      const { revealed } = session;
      const mine = answer?.questionId === revealed.questionId ? answer : null;
      return (
        <RevealedQuestion revealed={revealed}>
          <MyResult revealed={revealed} answer={mine} />
        </RevealedQuestion>
      );
    }
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
  return <p data-testid="answer-status">{REFUSALS[answer.result!] ?? "Your Answer was not accepted."}</p>;
}

function MyResult({ revealed, answer }: { revealed: QuestionRevealed; answer: MyAnswer | null }) {
  if (answer?.result === "JoinedAfterStart") {
    return <p data-testid="my-result">You joined during this Question.</p>;
  }
  if (answer?.result === "AlreadyAnswered") {
    // Answered earlier (e.g. before a reload): this tap isn't necessarily the option that counts.
    return <p data-testid="my-result">Your earlier Answer counts.</p>;
  }
  const outcome =
    answer?.status !== "locked" ? "Timed out" : answer.position === revealed.correctOption ? "Correct" : "Incorrect";
  return (
    <p data-testid="my-result" style={{ fontSize: "2rem", fontWeight: "bold" }}>
      {outcome}
    </p>
  );
}
