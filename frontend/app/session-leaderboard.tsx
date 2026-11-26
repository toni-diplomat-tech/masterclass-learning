import { Leaderboard } from "@/components/leaderboard";
import type { SessionLeaderboard } from "@/lib/live-masterclass";
import styles from "./quiz.module.scss";

/**
 * The Session leaderboard on the projector: rank, Nickname and points, highest first, the top `limit`
 * Participants (equal points share a rank). Final once the Session has ended. Nothing while there is none.
 */
export function SessionLeaderboardTable({ leaderboard, limit = 10 }: { leaderboard: SessionLeaderboard | null; limit?: number }) {
  if (!leaderboard) return null;
  const shown = leaderboard.entries.slice(0, limit);
  return (
    <section data-testid="session-leaderboard" className={`${styles.leaderboard} ${styles.projectorBoard}`}>
      <h2 className={styles.leaderboardTitle}>{leaderboard.ended ? "Final leaderboard" : "Leaderboard"}</h2>
      {shown.length === 0 ? (
        <p className={styles.muted}>No points yet.</p>
      ) : (
        <Leaderboard rows={shown} rowTestId="leaderboard-entry" large />
      )}
      {leaderboard.entries.length > shown.length && (
        <p className={styles.muted}>…and {leaderboard.entries.length - shown.length} more</p>
      )}
    </section>
  );
}
