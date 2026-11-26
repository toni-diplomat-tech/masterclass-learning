"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/components/button";
import { Card } from "@/components/card";
import { Notice } from "@/components/notice";
import type { WorkshopOverview } from "@/lib/courses";
import type { DifficultyCounts } from "@/lib/domain";
import styles from "./projector.module.scss";

const DIFFICULTIES = [
  { key: "easy", label: "Easy" },
  { key: "medium", label: "Medium" },
  { key: "hard", label: "Hard" },
] as const;

function suggestedMix(workshop: WorkshopOverview | undefined): DifficultyCounts {
  const counts = workshop?.questions ?? { easy: 0, medium: 0, hard: 0 };
  return { easy: Math.min(2, counts.easy), medium: Math.min(2, counts.medium), hard: Math.min(1, counts.hard) };
}

/**
 * The start-Session dialog: pick a Workshop, see how many Questions of each Difficulty it has, and set
 * the Difficulty mix. A mix larger than the Workshop's total can't be submitted (the server checks too);
 * a short Difficulty is filled from the others when the Session starts. A frosted sheet on the projector.
 */
export function StartSessionDialog({
  workshops,
  onStart,
}: {
  workshops: WorkshopOverview[];
  onStart: (workshopId: number, mix: DifficultyCounts) => Promise<string[]>;
}) {
  const [workshopId, setWorkshopId] = useState(workshops[0]?.id);
  const workshop = workshops.find((w) => w.id === workshopId);
  const [mix, setMix] = useState(() => suggestedMix(workshops[0]));
  const [problems, setProblems] = useState<string[]>([]);
  const [starting, setStarting] = useState(false);

  if (workshops.length === 0) return <p className={styles.muted}>This Course has no Workshops to run a Session for.</p>;

  const available = workshop ? workshop.questions.easy + workshop.questions.medium + workshop.questions.hard : 0;
  const total = mix.easy + mix.medium + mix.hard;
  const blocked =
    total > available
      ? `The mix asks for ${total} Questions but the Workshop has only ${available}.`
      : total === 0
        ? "Ask for at least one Question."
        : null;

  const chooseWorkshop = (id: number) => {
    setWorkshopId(id);
    setMix(suggestedMix(workshops.find((w) => w.id === id)));
    setProblems([]);
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (blocked || workshopId === undefined) return;
    setStarting(true);
    try {
      setProblems(await onStart(workshopId, mix));
    } catch {
      setProblems(["The Session could not be started. Try again."]);
    } finally {
      setStarting(false);
    }
  };

  return (
    <Card glass className={styles.dialog}>
      <form onSubmit={submit} data-testid="start-session" className={styles.dialogForm}>
        <h2>Start a Session</h2>
        <label className={styles.field}>
          Workshop
          <select value={workshopId} onChange={(event) => chooseWorkshop(Number(event.target.value))}>
            {workshops.map((w) => (
              <option key={w.id} value={w.id}>
                {w.number}. {w.title}
              </option>
            ))}
          </select>
        </label>
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
                  {workshop?.questions[key] ?? 0}
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
