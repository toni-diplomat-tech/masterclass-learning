import type { ReactNode } from "react";
import { OptionTile, OptionTiles } from "@/components/option-tile";
import { Chips } from "@/components/page";
import type { QuestionRevealed } from "@/lib/live-masterclass";
import styles from "./quiz.module.scss";

/**
 * A revealed Question on the projector (`large`) or a phone: the correct option marked, how many chose
 * each option, and how many answered or were Timed out. `children` go on top (the phone's own result).
 */
export function RevealedQuestion({
  revealed,
  large = false,
  children,
}: {
  revealed: QuestionRevealed;
  large?: boolean;
  children?: ReactNode;
}) {
  return (
    <section data-testid="revealed" className={[styles.question, large && styles.large].filter(Boolean).join(" ")}>
      <Chips items={[revealed.workshopTitle, `Question ${revealed.number} of ${revealed.questionCount}`, revealed.difficulty]} />
      {children}
      <h2 className={styles.questionText}>{revealed.text}</h2>
      <OptionTiles large={large}>
        {revealed.options.map((option) => {
          const correct = option.position === revealed.correctOption;
          return (
            <OptionTile
              key={option.position}
              testId="revealed-option"
              correct={correct}
              position={option.position}
              state={correct ? "correct" : "wrong"}
              count={option.answers}
            >
              {option.text}
            </OptionTile>
          );
        })}
      </OptionTiles>
      <p className={styles.muted}>
        {revealed.answered} answered · {revealed.timedOut} timed out
      </p>
      <p className={styles.muted}>{revealed.lastQuestion ? "That was the last Question." : "The next Question starts in a moment…"}</p>
    </section>
  );
}
