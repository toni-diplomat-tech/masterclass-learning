"use client";

import { useEffect, useState } from "react";
import type { QuestionStarted } from "@/lib/live";

/** Whole seconds until the deadline (never below 0), ticking. For display only: the server decides. */
function useSecondsLeft(deadline: string): number {
  const deadlineMs = Date.parse(deadline);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 200);
    return () => clearInterval(timer);
  }, [deadlineMs]);

  return Math.max(0, Math.ceil((deadlineMs - now) / 1000));
}

/** A running Question with its options and a countdown, on the projector (`large`) or a phone. */
export function LiveQuestion({ question, large = false }: { question: QuestionStarted; large?: boolean }) {
  const secondsLeft = useSecondsLeft(question.deadline);

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
            {option.text}
          </li>
        ))}
      </ol>
    </section>
  );
}
