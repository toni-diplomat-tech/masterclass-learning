import type { HubConnection } from "@microsoft/signalr";

// Browser-side: the Session leaderboard on the Quiz hub (backend Live/QuizHub.cs, Quiz/Leaderboards.cs).

/** One Participant on a leaderboard. Equal points share a rank (1, 1, 3). */
export type LeaderboardEntry = {
  rank: number;
  participantId: number;
  nickname: string;
  /** The Session's points so far (revealed Questions only). */
  points: number;
  /** Points scored on the latest revealed Question. */
  questionPoints: number;
};

/** Server → clients after each reveal, and once more (final, `ended`) when the Session ends. Highest points first. */
export type SessionLeaderboard = {
  masterclassId: number;
  sessionId: number;
  workshopTitle: string;
  ended: boolean;
  entries: LeaderboardEntry[];
};

export const LEADERBOARD_UPDATED = "LeaderboardUpdated";

/** Keeps `setLeaderboard` on the Masterclass's latest Session leaderboard. Register before starting the connection. */
export function followLeaderboard(
  connection: HubConnection,
  masterclassId: number,
  setLeaderboard: (leaderboard: SessionLeaderboard) => void,
): void {
  connection.on(LEADERBOARD_UPDATED, (leaderboard: SessionLeaderboard) => {
    if (leaderboard.masterclassId === masterclassId) setLeaderboard(leaderboard);
  });
}

/** The leaderboard if it belongs to `sessionId` (one from an earlier Session is stale). */
export function leaderboardFor(leaderboard: SessionLeaderboard | null, sessionId: number): SessionLeaderboard | null {
  return leaderboard?.sessionId === sessionId ? leaderboard : null;
}
