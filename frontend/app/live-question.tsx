"use client";

import { useEffect, useState, type ReactNode } from "react";
import { OptionTile, OptionTiles } from "@/components/option-tile";
import { Chips } from "@/components/page";
import type { QuestionStarted } from "@/lib/live-masterclass";
import styles from "./quiz.module.scss";

/** Whole seconds until the deadline (never below 0), ticking. For display only: the server decides. */
function useSecondsLeft(deadline: string): number {
  const deadlineMs = Date.parse(deadline);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 200);
    return () => clearInterval(timer);
  }, []);

  return Math.max(0, Math.ceil((deadlineMs - now) / 1000));
}

const HURRY_SECONDS = 5;

/**
 * A running Question with its options and a countdown, on the projector (`large`) or a phone. With
 * `onChoose` (the phone), the options are buttons until one is `chosen`, they are `locked` (the phone
 * has an Answer, or joined too late to give one) or time is up. `footer` goes under the options (the
 * projector's answer count, the phone's "Answer locked").
 */
export function LiveQuestion({
  question,
  large = false,
  chosen = null,
  locked: answerLocked = false,
  onChoose,
  footer,
}: {
  question: QuestionStarted;
  large?: boolean;
  chosen?: number | null;
  locked?: boolean;
  onChoose?: (position: number) => void;
  footer?: ReactNode;
}) {
  const secondsLeft = useSecondsLeft(question.deadline);
  const locked = answerLocked || chosen !== null || secondsLeft === 0;
  const countdownClass = [styles.countdown, secondsLeft === 0 ? styles.timeUp : secondsLeft <= HURRY_SECONDS && styles.hurry]
    .filter(Boolean)
    .join(" ");

  return (
    <section data-testid="question" className={[styles.question, large && styles.large].filter(Boolean).join(" ")}>
      <div className={styles.top}>
        <Chips items={[question.workshopTitle, `Question ${question.number} of ${question.questionCount}`, question.difficulty]} />
        <p data-testid="countdown" aria-live="polite" className={countdownClass}>
          {secondsLeft > 0 ? secondsLeft : "Time's up"}
        </p>
      </div>
      <h2 data-testid="question-text" className={styles.questionText}>
        {question.text}
      </h2>
      <OptionTiles large={large}>
        {question.options.map((option) => (
          <OptionTile
            key={option.position}
            testId="option"
            position={option.position}
            state={chosen === option.position ? "selected" : "idle"}
            disabled={locked}
            onChoose={onChoose && (() => onChoose(option.position))}
          >
            {option.text}
          </OptionTile>
        ))}
      </OptionTiles>
      {footer && <div className={styles.footer}>{footer}</div>}
    </section>
  );
}
