import type { SessionLeaderboard } from "@/lib/live-masterclass";
import styles from "../participant.module.scss";
import { RankCard } from "./rank-card";

/**
 * This Participant's score on their phone, from the Session leaderboard: after a reveal, the points the
 * Question earned and the Session total; once the Session has ended, the total and their rank.
 */
export function MyScore({
  leaderboard,
  participantId,
  sessionOver = false,
}: {
  leaderboard: SessionLeaderboard | null;
  participantId: number | null;
  sessionOver?: boolean;
}) {
  if (!leaderboard || participantId === null) return null;
  const me = leaderboard.entries.find((entry) => entry.participantId === participantId);
  if (!me) return null;

  if (leaderboard.ended || sessionOver) {
    return <RankCard testId="my-score" points={me.points} rank={me.rank} of={leaderboard.entries.length} />;
  }

  return (
    <p data-testid="my-score" className={styles.scoreLine}>
      +{me.questionPoints} points · {me.points} in this Session
    </p>
  );
}
