import type { SessionLeaderboard } from "@/lib/leaderboard";

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
    return (
      <section data-testid="my-score" style={{ fontSize: "1.5rem" }}>
        <p style={{ fontSize: "2rem", fontWeight: "bold", margin: "0.5rem 0" }}>
          {me.points} {me.points === 1 ? "point" : "points"}
        </p>
        <p data-testid="my-rank" style={{ margin: "0.5rem 0" }}>
          Rank {me.rank} of {leaderboard.entries.length}
        </p>
      </section>
    );
  }

  return (
    <p data-testid="my-score" style={{ fontSize: "1.25rem" }}>
      +{me.questionPoints} points · {me.points} in this Session
    </p>
  );
}
