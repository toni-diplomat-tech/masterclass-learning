import type { ReactNode } from "react";
import type { QuestionRevealed } from "@/lib/live-masterclass";

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
    <section data-testid="revealed" style={{ maxWidth: "60rem", margin: "0 auto" }}>
      <p>
        {revealed.workshopTitle} · Question {revealed.number} of {revealed.questionCount} · {revealed.difficulty}
      </p>
      {children}
      <h2 style={{ fontSize: large ? "2.5rem" : "1.5rem" }}>{revealed.text}</h2>
      <ol style={{ listStyle: "upper-alpha", textAlign: "left", fontSize: large ? "2rem" : "1.25rem", padding: "0 0 0 2em" }}>
        {revealed.options.map((option) => {
          const correct = option.position === revealed.correctOption;
          return (
            <li
              key={option.position}
              data-testid="revealed-option"
              data-correct={correct}
              style={{ margin: "0.5rem 0", fontWeight: correct ? "bold" : "normal", opacity: correct ? 1 : 0.6 }}
            >
              {option.text} {correct && "✓"}
              <span data-testid="option-answers" style={{ float: "right" }}>
                {option.answers}
              </span>
            </li>
          );
        })}
      </ol>
      <p>
        {revealed.answered} answered · {revealed.timedOut} timed out
      </p>
      <p>{revealed.lastQuestion ? "That was the last Question." : "The next Question starts in a moment…"}</p>
    </section>
  );
}
