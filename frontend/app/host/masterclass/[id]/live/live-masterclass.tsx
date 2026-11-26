"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { LiveQuestion } from "@/app/live-question";
import { RevealedQuestion } from "@/app/revealed-question";
import type { WorkshopOverview } from "@/lib/courses";
import { useLiveProjector } from "@/lib/live-masterclass";
import { ResumeButton } from "./resume-button";
import { SessionLeaderboardTable } from "@/app/session-leaderboard";
import { StartSessionDialog } from "./start-session-dialog";

/**
 * The projector of an Open Masterclass, following it live: the Join invitation (`children`), the live
 * number of Participants and the start-Session dialog; then the running Question with its countdown and
 * answer count, its reveal, and so on until the Session ends, when the start dialog comes back for the
 * next Workshop.
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
  const { session, leaderboard, connected, closed, participants: count, startSession, resume } = useLiveProjector(
    masterclassId,
    initialParticipants,
  );
  const router = useRouter();

  // Closed (e.g. from another tab): the page renders again as the closed projector.
  useEffect(() => {
    if (closed) router.refresh();
  }, [closed, router]);

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
        <SessionLeaderboardTable leaderboard={leaderboard} />
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
      {session.step === "ended" && <SessionLeaderboardTable leaderboard={leaderboard} />}
      {children}
      {participantCount}
      <StartSessionDialog workshops={workshops} onStart={startSession} />
    </>
  );
}
