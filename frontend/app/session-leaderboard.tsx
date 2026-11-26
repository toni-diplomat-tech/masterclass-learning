import type { SessionLeaderboard } from "@/lib/live-masterclass";

/**
 * The Session leaderboard on the projector: rank, Nickname and points, highest first, the top `limit`
 * Participants (equal points share a rank). Final once the Session has ended. Nothing while there is none.
 */
export function SessionLeaderboardTable({ leaderboard, limit = 10 }: { leaderboard: SessionLeaderboard | null; limit?: number }) {
  if (!leaderboard) return null;
  const shown = leaderboard.entries.slice(0, limit);
  return (
    <section data-testid="session-leaderboard" style={{ maxWidth: "40rem", margin: "1rem auto" }}>
      <h2 style={{ fontSize: "2rem" }}>{leaderboard.ended ? "Final leaderboard" : "Leaderboard"}</h2>
      {shown.length === 0 ? (
        <p>No points yet.</p>
      ) : (
        <table style={{ width: "100%", fontSize: "1.5rem", borderCollapse: "collapse" }}>
          <tbody>
            {shown.map((entry) => (
              <tr key={entry.participantId} data-testid="leaderboard-entry">
                <td style={{ textAlign: "right", paddingRight: "1rem", width: "3rem" }}>{entry.rank}.</td>
                <td style={{ textAlign: "left" }}>{entry.nickname}</td>
                <td style={{ textAlign: "right" }}>{entry.points}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {leaderboard.entries.length > shown.length && <p>…and {leaderboard.entries.length - shown.length} more</p>}
    </section>
  );
}
