import { Card } from "@/components/card";
import styles from "../participant.module.scss";

/**
 * The Participant's points and rank out of everyone on a leaderboard, on their phone: the Session's once it
 * has ended, or the Masterclass's once it is Masterclass Closed. `where` names the leaderboard, e.g. "in this Masterclass".
 */
export function RankCard({
  points,
  rank,
  of,
  where,
  testId,
}: {
  points: number;
  rank: number;
  of: number;
  where?: string;
  testId: string;
}) {
  return (
    <Card data-testid={testId} className={styles.score}>
      <p className={styles.points}>
        {points} {points === 1 ? "point" : "points"}
      </p>
      <p data-testid={`${testId}-rank`} className={styles.label}>
        Rank {rank} of {of}
        {where && ` ${where}`}
      </p>
    </Card>
  );
}
