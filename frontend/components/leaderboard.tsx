import type { CSSProperties } from "react";
import styles from "./leaderboard.module.scss";

export type LeaderboardRow = { participantId: number; rank: number; nickname: string; points: number };

/**
 * Leaderboard rows: rank, Nickname and points, the podium (ranks 1–3) highlighted. `large` on the
 * projector; `highlight` marks one Participant's own row. Each row carries `rowTestId`.
 */
export function Leaderboard({
  rows,
  rowTestId,
  large = false,
  highlight,
}: {
  rows: LeaderboardRow[];
  rowTestId: string;
  large?: boolean;
  highlight?: number | null;
}) {
  return (
    <ol className={[styles.board, large && styles.large].filter(Boolean).join(" ")}>
      {rows.map((row, index) => (
        <li
          key={row.participantId}
          data-testid={rowTestId}
          className={[styles.row, row.rank <= 3 && styles[`rank${row.rank}`], row.participantId === highlight && styles.me]
            .filter(Boolean)
            .join(" ")}
          style={{ "--index": index } as CSSProperties}
        >
          <span className={styles.rank}>{row.rank}.</span>
          <span className={styles.nickname}>{row.nickname}</span>
          <span className={styles.points}>{row.points}</span>
        </li>
      ))}
    </ol>
  );
}
