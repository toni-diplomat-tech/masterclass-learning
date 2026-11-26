import type { MasterclassLeaderboard } from "@/lib/masterclasses";

/**
 * The Masterclass leaderboard on the projector at the end of the meeting: rank, Nickname and points summed
 * over all Sessions, highest first (equal points share a rank).
 */
export function MasterclassLeaderboardTable({ leaderboard }: { leaderboard: MasterclassLeaderboard }) {
  return (
    <section data-testid="masterclass-leaderboard" style={{ maxWidth: "40rem", margin: "1rem auto" }}>
      <h2 style={{ fontSize: "2.5rem" }}>Masterclass leaderboard</h2>
      {leaderboard.entries.length === 0 ? (
        <p>Nobody scored in this Masterclass.</p>
      ) : (
        <table style={{ width: "100%", fontSize: "1.75rem", borderCollapse: "collapse" }}>
          <tbody>
            {leaderboard.entries.map((entry) => (
              <tr key={entry.participantId} data-testid="masterclass-leaderboard-entry">
                <td style={{ textAlign: "right", paddingRight: "1rem", width: "3rem" }}>{entry.rank}.</td>
                <td style={{ textAlign: "left" }}>{entry.nickname}</td>
                <td style={{ textAlign: "right" }}>{entry.points}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
