"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { LiveQuestion } from "@/app/live-question";
import { RevealedQuestion } from "@/app/revealed-question";
import { SessionLeaderboardTable } from "@/app/session-leaderboard";
import type { WorkshopOverview } from "@/lib/courses";
import { useLiveProjector } from "@/lib/live-masterclass";
import styles from "./projector.module.scss";
import { ResumeButton } from "./resume-button";
import { StartSessionDialog } from "./start-session-dialog";

/**
 * The projector of an Open Masterclass, following it live: the Join invitation (`children`), the live
 * number of Participants and the start-Session dialog; then the running Question with its countdown and
 * answer count, its reveal, and so on until the Session ends, when the start dialog comes back for the
 * next Workshop. `closeControl` (Close Masterclass) sits at the bottom between Sessions.
 */
export function LiveMasterclass({
  masterclassId,
  initialParticipants,
  workshops,
  closeControl,
  children,
}: {
  masterclassId: number;
  initialParticipants: number;
  workshops: WorkshopOverview[];
  closeControl: ReactNode;
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
    <div className={running ? styles.compact : undefined}>
      <p className={styles.count}>
        <span data-testid="participant-count" className={styles.countNumber}>
          {count}
        </span>{" "}
        <span className={styles.countLabel}>{count === 1 ? "Participant" : "Participants"} joined</span>
      </p>
      {!connected && <small>Reconnecting to live updates…</small>}
    </div>
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
            <p data-testid="answer-count" className={styles.answerCount}>
              {answered}
              {participants !== undefined && ` of ${participants}`} answered
            </p>
          }
        />
        <div className={styles.controls}>
          {participantCount}
          <ResumeButton onResume={resume} />
        </div>
      </>
    );
  }

  if (session.step === "revealed") {
    return (
      <>
        <RevealedQuestion revealed={session.revealed} large />
        <SessionLeaderboardTable leaderboard={leaderboard} />
        <div className={styles.controls}>
          {participantCount}
          <ResumeButton onResume={resume} />
        </div>
      </>
    );
  }

  return (
    <>
      {session.step === "ended" && (
        <h2 data-testid="session-over" className={styles.sessionOver}>
          Session over
        </h2>
      )}
      {session.step === "ended" && <SessionLeaderboardTable leaderboard={leaderboard} />}
      {children}
      {participantCount}
      <StartSessionDialog workshops={workshops} onStart={startSession} />
      {closeControl}
    </>
  );
}
