"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/components/button";
import { Card } from "@/components/card";
import { Notice, type NoticeTone } from "@/components/notice";
import { Eyebrow } from "@/components/page";
import type { WorkshopOverview } from "@/lib/courses";
import type { DifficultyCounts } from "@/lib/domain";
import { workshopProgress, type WorkshopStep } from "@/lib/workshop-progress";
import styles from "./projector.module.scss";

const DIFFICULTIES = [
  { key: "easy", label: "Easy" },
  { key: "medium", label: "Medium" },
  { key: "hard", label: "Hard" },
] as const;

const STEP_LABELS: Record<WorkshopStep, string> = { done: "Done", next: "Next", locked: "Locked" };
const STEP_TONES: Record<WorkshopStep, NoticeTone> = { done: "success", next: "primary", locked: "info" };

function suggestedMix(workshop: WorkshopOverview | undefined): DifficultyCounts {
  const counts = workshop?.questions ?? { easy: 0, medium: 0, hard: 0 };
  return { easy: Math.min(2, counts.easy), medium: Math.min(2, counts.medium), hard: Math.min(1, counts.hard) };
}

/**
 * The start-Session dialog: where the Masterclass has got to in its Course (Workshops that have had their
 * Session done, the Next Workshop next, the rest locked), and the Difficulty mix for the Next Workshop's Session, the only
 * one that can start, beside how many Questions of each Difficulty it has. A mix larger than the
 * Workshop's total can't be submitted (the server checks too); a short Difficulty is filled from the
 * others when the Session starts. A frosted sheet on the projector. Key it by the Next Workshop so the mix
 * starts afresh when the Next Workshop moves on.
 */
export function StartSessionDialog({
  workshops,
  nextWorkshopId,
  workshopIdsWithSession,
  onStart,
}: {
  workshops: WorkshopOverview[];
  nextWorkshopId: number | null;
  workshopIdsWithSession: number[];
  onStart: (workshopId: number, mix: DifficultyCounts) => Promise<string[]>;
}) {
  const progress = workshopProgress(workshops, nextWorkshopId, workshopIdsWithSession);
  const nextWorkshop = progress.find((p) => p.step === "next")?.workshop;
  const [mix, setMix] = useState(() => suggestedMix(nextWorkshop));
  const [problems, setProblems] = useState<string[]>([]);
  const [starting, setStarting] = useState(false);

  if (workshops.length === 0) return <p className={styles.muted}>This Course has no Workshops to run a Session for.</p>;

  const courseProgress = (
    <ol data-testid="workshop-progress" className={styles.workshops}>
      {progress.map(({ workshop, step }) => (
        <li key={workshop.id} data-step={step}>
          <Notice tone={STEP_TONES[step]} className={styles.workshop}>
            {step === "next" ? (
              <strong>
                {workshop.number}. {workshop.title}
              </strong>
            ) : (
              <span>
                {workshop.number}. {workshop.title}
              </span>
            )}
            <Eyebrow>{STEP_LABELS[step]}</Eyebrow>
          </Notice>
        </li>
      ))}
    </ol>
  );

  if (!nextWorkshop) {
    return (
      <Card glass className={styles.dialog}>
        <div className={styles.dialogForm}>
          <h2>Every Workshop has had its Session</h2>
          {courseProgress}
        </div>
      </Card>
    );
  }

  const available = nextWorkshop.questions.easy + nextWorkshop.questions.medium + nextWorkshop.questions.hard;
  const total = mix.easy + mix.medium + mix.hard;
  const blocked =
    total > available
      ? `The mix asks for ${total} Questions but the Workshop has only ${available}.`
      : total === 0
        ? "Ask for at least one Question."
        : null;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (blocked) return;
    setStarting(true);
    try {
      setProblems(await onStart(nextWorkshop.id, mix));
    } catch {
      setProblems(["The Session could not be started. Try again."]);
    } finally {
      setStarting(false);
    }
  };

  return (
    <Card glass className={styles.dialog}>
      <form onSubmit={submit} data-testid="start-session" className={styles.dialogForm}>
        <h2>
          Start the Session for {nextWorkshop.number}. {nextWorkshop.title}
        </h2>
        {courseProgress}
        <table>
          <thead>
            <tr>
              <th scope="col">Difficulty</th>
              <th scope="col" className={styles.num}>
                In the Workshop
              </th>
              <th scope="col" className={styles.num}>
                Questions to ask
              </th>
            </tr>
          </thead>
          <tbody>
            {DIFFICULTIES.map(({ key, label }) => (
              <tr key={key}>
                <th scope="row">{label}</th>
                <td data-testid={`available-${key}`} className={styles.num}>
                  {nextWorkshop.questions[key]}
                </td>
                <td className={styles.num}>
                  <input
                    type="number"
                    min={0}
                    max={available}
                    value={mix[key]}
                    aria-label={`${label} Questions to ask`}
                    onChange={(event) => setMix({ ...mix, [key]: Math.max(0, Math.floor(Number(event.target.value) || 0)) })}
                    className={styles.mixInput}
                  />
                </td>
              </tr>
            ))}
            <tr>
              <th scope="row">Total</th>
              <td className={styles.num}>{available}</td>
              <td className={styles.num}>{total}</td>
            </tr>
          </tbody>
        </table>
        <p className={styles.hint}>A Difficulty with too few Questions is filled from the others.</p>
        {blocked && (
          <Notice tone="error" role="alert">
            {blocked}
          </Notice>
        )}
        {problems.map((problem) => (
          <Notice key={problem} tone="error" role="alert">
            {problem}
          </Notice>
        ))}
        <Button type="submit" size="lg" block disabled={blocked !== null} loading={starting}>
          {starting ? "Starting…" : "Start Session"}
        </Button>
      </form>
    </Card>
  );
}
