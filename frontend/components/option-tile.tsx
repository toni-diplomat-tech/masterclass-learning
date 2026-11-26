import type { ReactNode } from "react";
import styles from "./option-tile.module.scss";

export type OptionTileState = "idle" | "selected" | "correct" | "wrong";

/** A, B, C… for option positions 1, 2, 3… */
export function optionLetter(position: number): string {
  return String.fromCharCode(64 + position);
}

/** The grid of answer option tiles: one column on a phone, two on the projector (`large`). */
export function OptionTiles({ large = false, children }: { large?: boolean; children: ReactNode }) {
  return <ol className={[styles.tiles, large && styles.large].filter(Boolean).join(" ")}>{children}</ol>;
}

/**
 * One answer option: its letter, its text and, once revealed, how many chose it (`count`). With
 * `onChoose` it is a button (the phone). `state` colours it: selected (red), correct (green) or wrong (muted).
 */
export function OptionTile({
  position,
  state = "idle",
  disabled = false,
  onChoose,
  count,
  testId,
  correct,
  children,
}: {
  position: number;
  state?: OptionTileState;
  disabled?: boolean;
  onChoose?: () => void;
  count?: number;
  testId: string;
  correct?: boolean;
  children: ReactNode;
}) {
  const className = `${styles.tile} ${styles[state]}`;
  const content = (
    <>
      <span className={styles.letter} aria-hidden="true">
        {state === "correct" ? "✓" : optionLetter(position)}
      </span>
      <span className={styles.text}>
        <span className={styles.srOnly}>{optionLetter(position)}. </span>
        {children}
        {state === "correct" && <span className={styles.srOnly}> (correct)</span>}
      </span>
      {count !== undefined && (
        <span className={styles.count} data-testid="option-answers">
          {count}
        </span>
      )}
    </>
  );

  return (
    <li data-testid={testId} data-correct={correct} className={styles.item}>
      {onChoose ? (
        <button type="button" className={className} disabled={disabled} aria-pressed={state === "selected"} onClick={onChoose}>
          {content}
        </button>
      ) : (
        <div className={className}>{content}</div>
      )}
    </li>
  );
}
