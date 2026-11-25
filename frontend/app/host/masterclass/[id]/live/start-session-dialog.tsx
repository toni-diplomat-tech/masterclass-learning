"use client";

import { useState } from "react";
import type { WorkshopOverview } from "@/lib/courses";
import type { DifficultyMix } from "@/lib/live";

const DIFFICULTIES = [
  { key: "easy", label: "Easy" },
  { key: "medium", label: "Medium" },
  { key: "hard", label: "Hard" },
] as const;

function suggestedMix(workshop: WorkshopOverview | undefined): DifficultyMix {
  const counts = workshop?.questions ?? { easy: 0, medium: 0, hard: 0 };
  return { easy: Math.min(2, counts.easy), medium: Math.min(2, counts.medium), hard: Math.min(1, counts.hard) };
}

/**
 * The start-Session dialog: pick a Workshop, see how many Questions of each Difficulty it has, and set
 * the Difficulty mix. A mix larger than the Workshop's total can't be submitted (the server checks too);
 * a short Difficulty is filled from the others when the Session starts.
 */
export function StartSessionDialog({
  workshops,
  onStart,
}: {
  workshops: WorkshopOverview[];
  onStart: (workshopId: number, mix: DifficultyMix) => Promise<string[]>;
}) {
  const [workshopId, setWorkshopId] = useState(workshops[0]?.id);
  const workshop = workshops.find((w) => w.id === workshopId);
  const [mix, setMix] = useState(() => suggestedMix(workshops[0]));
  const [problems, setProblems] = useState<string[]>([]);
  const [starting, setStarting] = useState(false);

  if (workshops.length === 0) return <p>This Course has no Workshops to run a Session for.</p>;

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

  const submit = async (event: React.FormEvent) => {
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
    <form onSubmit={submit} data-testid="start-session" style={{ display: "inline-block", textAlign: "left", border: "1px solid", padding: "1rem 1.5rem", marginTop: "1rem" }}>
      <h2 style={{ marginTop: 0 }}>Start a Session</h2>
      <p>
        <label>
          Workshop{" "}
          <select value={workshopId} onChange={(event) => chooseWorkshop(Number(event.target.value))}>
            {workshops.map((w) => (
              <option key={w.id} value={w.id}>
                {w.number}. {w.title}
              </option>
            ))}
          </select>
        </label>
      </p>
      <table>
        <thead>
          <tr>
            <th scope="col">Difficulty</th>
            <th scope="col">In the Workshop</th>
            <th scope="col">Questions to ask</th>
          </tr>
        </thead>
        <tbody>
          {DIFFICULTIES.map(({ key, label }) => (
            <tr key={key}>
              <th scope="row">{label}</th>
              <td data-testid={`available-${key}`}>{workshop?.questions[key] ?? 0}</td>
              <td>
                <input
                  type="number"
                  min={0}
                  max={available}
                  value={mix[key]}
                  aria-label={`${label} Questions to ask`}
                  onChange={(event) => setMix({ ...mix, [key]: Math.max(0, Math.floor(Number(event.target.value) || 0)) })}
                  style={{ width: "4rem" }}
                />
              </td>
            </tr>
          ))}
          <tr>
            <th scope="row">Total</th>
            <td>{available}</td>
            <td>{total}</td>
          </tr>
        </tbody>
      </table>
      <p style={{ fontSize: "0.9rem" }}>A Difficulty with too few Questions is filled from the others.</p>
      {blocked && (
        <p role="alert" style={{ color: "crimson" }}>
          {blocked}
        </p>
      )}
      {problems.map((problem) => (
        <p key={problem} role="alert" style={{ color: "crimson" }}>
          {problem}
        </p>
      ))}
      <button type="submit" disabled={blocked !== null || starting} style={{ fontSize: "1.25rem" }}>
        {starting ? "Starting…" : "Start Session"}
      </button>
    </form>
  );
}
