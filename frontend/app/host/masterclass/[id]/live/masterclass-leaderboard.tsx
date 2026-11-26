import { Leaderboard } from "@/components/leaderboard";
import type { MasterclassLeaderboard } from "@/lib/masterclasses";
import quiz from "@/app/quiz.module.scss";

/**
 * The Masterclass leaderboard on the projector at the end of the meeting: rank, Nickname and points summed
 * over all Sessions, highest first (equal points share a rank).
 */
export function MasterclassLeaderboardTable({ leaderboard }: { leaderboard: MasterclassLeaderboard }) {
  return (
    <section data-testid="masterclass-leaderboard" className={`${quiz.leaderboard} ${quiz.projectorBoard}`}>
      <h2 className={quiz.leaderboardTitle}>Masterclass leaderboard</h2>
      {leaderboard.entries.length === 0 ? (
        <p className={quiz.muted}>Nobody scored in this Masterclass.</p>
      ) : (
        <Leaderboard rows={leaderboard.entries} rowTestId="masterclass-leaderboard-entry" large />
      )}
    </section>
  );
}
