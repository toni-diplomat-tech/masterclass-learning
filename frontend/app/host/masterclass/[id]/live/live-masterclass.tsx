"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { LiveQuestion } from "@/app/live-question";
import { RevealedQuestion } from "@/app/revealed-question";
import type { WorkshopOverview } from "@/lib/courses";
import { followSession, keepFollowing, startSession, type LiveSession } from "@/lib/live";
import { getCurrentState, newerSession, resumeSession } from "@/lib/current-state";
import { connectToQuizHub } from "@/lib/quiz-hub";
import { ResumeButton } from "./resume-button";
import { StartSessionDialog } from "./start-session-dialog";

type MasterclassSnapshot = { participants: number } | null;
type ParticipantCountUpdate = { masterclassId: number; participants: number };

/**
 * The projector of an Open Masterclass, following it over SignalR: the Join invitation (`children`),
 * the live number of Participants and the start-Session dialog; then the running Question with its
 * countdown and answer count, its reveal, and so on until the Session ends, when the start dialog
 * comes back for the next Workshop. It asks again for the current count on every (re)connect, since
 * SignalR doesn't replay messages missed while away.
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
  const [session, setSession] = useState<LiveSession>({ step: "waiting" });
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = connectToQuizHub();
    connectionRef.current = connection;
    // The count only grows; an update that overtook a later one must not lower it.
    const show = (participants: number) => setCount((shown) => Math.max(shown, participants));

    connection.on("ParticipantCountUpdated", (update: ParticipantCountUpdate) => {
      if (update.masterclassId === masterclassId) show(update.participants);
    });
    followSession(connection, masterclassId, setSession);

    const stop = keepFollowing(
      connection,
      async () => {
        const snapshot = await connection.invoke<MasterclassSnapshot>("WatchMasterclass", masterclassId);
        if (snapshot) show(snapshot.participants);
        // Where the Session stands now (after a reload, or broadcasts missed while reconnecting).
        const state = await getCurrentState(connection, masterclassId);
        if (state) setSession((shown) => newerSession(shown, state));
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

  const resume = async () => {
    const connection = connectionRef.current;
    if (!connection) throw new Error("Not connected");
    const state = await resumeSession(connection, masterclassId);
    if (state) setSession((shown) => newerSession(shown, state));
  };

  const running = session.step === "question" || session.step === "revealed";
  const participantCount = (
    <p style={{ fontSize: running ? "1.25rem" : "2rem" }}>
      <span data-testid="participant-count">{count}</span> {count === 1 ? "Participant" : "Participants"} joined
      {!connected && <small style={{ display: "block", fontSize: "1rem" }}>Reconnecting to live updates…</small>}
    </p>
  );

  if (session.step === "question") {
    const answered = session.count?.answered ?? 0;
    const participants = session.count?.participants;
    return (
      <>
        <LiveQuestion
          question={session.question}
          large
          footer={
            <p data-testid="answer-count" style={{ fontSize: "2rem" }}>
              {answered}
              {participants !== undefined && ` of ${participants}`} answered
            </p>
          }
        />
        {participantCount}
        <ResumeButton onResume={resume} />
      </>
    );
  }

  if (session.step === "revealed") {
    return (
      <>
        <RevealedQuestion revealed={session.revealed} large />
        {participantCount}
        <ResumeButton onResume={resume} />
      </>
    );
  }

  return (
    <>
      {session.step === "ended" && (
        <h2 data-testid="session-over" style={{ fontSize: "2.5rem" }}>
          Session over
        </h2>
      )}
      {children}
      {participantCount}
      <StartSessionDialog workshops={workshops} onStart={start} />
    </>
  );
}
