"use client";

import { useEffect, useState, type ReactNode } from "react";
import type { QuestionStarted } from "@/lib/live-masterclass";

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

  return (
    <section data-testid="question" style={{ maxWidth: "60rem", margin: "0 auto" }}>
      <p>
        {question.workshopTitle} · Question {question.number} of {question.questionCount} · {question.difficulty}
      </p>
      <p data-testid="countdown" aria-live="polite" style={{ fontSize: large ? "4rem" : "2.5rem", fontWeight: "bold", margin: "0.5rem 0" }}>
        {secondsLeft > 0 ? secondsLeft : "Time's up"}
      </p>
      <h2 data-testid="question-text" style={{ fontSize: large ? "2.5rem" : "1.5rem" }}>
        {question.text}
      </h2>
      <ol style={{ listStyle: "upper-alpha", textAlign: "left", fontSize: large ? "2rem" : "1.25rem", padding: "0 0 0 2em" }}>
        {question.options.map((option) => (
          <li key={option.position} data-testid="option" style={{ margin: "0.5rem 0" }}>
            {onChoose ? (
              <button
                type="button"
                disabled={locked}
                aria-pressed={chosen === option.position}
                onClick={() => onChoose(option.position)}
                style={{
                  width: "100%",
                  padding: "0.75rem",
                  fontSize: "inherit",
                  textAlign: "left",
                  fontWeight: chosen === option.position ? "bold" : "normal",
                  outline: chosen === option.position ? "3px solid currentColor" : undefined,
                }}
              >
                {option.text}
              </button>
            ) : (
              option.text
            )}
          </li>
        ))}
      </ol>
      {footer}
    </section>
  );
}
