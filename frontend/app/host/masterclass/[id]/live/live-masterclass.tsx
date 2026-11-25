"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { LiveQuestion } from "@/app/live-question";
import type { WorkshopOverview } from "@/lib/courses";
import { keepFollowing, QUESTION_STARTED, startSession, type QuestionStarted } from "@/lib/live";
import { connectToQuizHub } from "@/lib/quiz-hub";
import { StartSessionDialog } from "./start-session-dialog";

type MasterclassSnapshot = { participants: number } | null;
type ParticipantCountUpdate = { masterclassId: number; participants: number };

/**
 * The projector of an Open Masterclass, following it over SignalR: the Join invitation (`children`),
 * the live number of Participants and the start-Session dialog, then the running Question with its
 * countdown. It asks again for the current count on every (re)connect, since SignalR doesn't replay
 * messages missed while away.
 */
export function LiveMasterclass({
  masterclassId,
  initialParticipants,
  workshops,
  children,
}: {
  masterclassId: number;
  initialParticipants: number;
  workshops: WorkshopOverview[];
  children: ReactNode;
}) {
  const [count, setCount] = useState(initialParticipants);
  const [connected, setConnected] = useState(false);
  const [question, setQuestion] = useState<QuestionStarted | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = connectToQuizHub();
    connectionRef.current = connection;
    // The count only grows; an update that overtook a later one must not lower it.
    const show = (participants: number) => setCount((shown) => Math.max(shown, participants));

    connection.on("ParticipantCountUpdated", (update: ParticipantCountUpdate) => {
      if (update.masterclassId === masterclassId) show(update.participants);
    });
    connection.on(QUESTION_STARTED, (started: QuestionStarted) => {
      if (started.masterclassId === masterclassId) setQuestion(started);
    });

    const stop = keepFollowing(
      connection,
      async () => {
        const snapshot = await connection.invoke<MasterclassSnapshot>("WatchMasterclass", masterclassId);
        if (snapshot) show(snapshot.participants);
      },
      setConnected,
    );
    return () => {
      connectionRef.current = null;
      stop();
    };
  }, [masterclassId]);

  const start = async (workshopId: number, mix: { easy: number; medium: number; hard: number }) => {
    const connection = connectionRef.current;
    if (!connection || !connected) return ["Not connected to live updates yet. Try again in a moment."];
    return startSession(connection, masterclassId, workshopId, mix);
  };

  const participantCount = (
    <p style={{ fontSize: question ? "1.25rem" : "2rem" }}>
      <span data-testid="participant-count">{count}</span> {count === 1 ? "Participant" : "Participants"} joined
      {!connected && <small style={{ display: "block", fontSize: "1rem" }}>Reconnecting to live updates…</small>}
    </p>
  );

  if (question) {
    return (
      <>
        <LiveQuestion question={question} large />
        {participantCount}
      </>
    );
  }

  return (
    <>
      {children}
      {participantCount}
      <StartSessionDialog workshops={workshops} onStart={start} />
    </>
  );
}
