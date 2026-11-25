"use client";

import { useActionState } from "react";
import { importCourseFile, type ImportState } from "./actions";

const initialState: ImportState = {};

export function ImportForm() {
  const [state, formAction, pending] = useActionState(importCourseFile, initialState);

  return (
    <form action={formAction}>
      <p>
        <label htmlFor="import-file">Course import file</label>{" "}
        <input id="import-file" name="file" type="file" accept=".json,application/json" required />{" "}
        <button type="submit" disabled={pending}>
          {pending ? "Importing…" : "Import"}
        </button>
      </p>
      {state.summary && (
        <p role="status" data-testid="import-summary">
          Imported {state.summary.courseTitle}: {state.summary.workshops} Workshops, {state.summary.lessons} Lessons,{" "}
          {state.summary.questions} Questions ({state.summary.questionsAdded} added, {state.summary.questionsUpdated}{" "}
          updated, {state.summary.questionsRetired} retired).
        </p>
      )}
      {state.problems && (
        <div role="alert" data-testid="import-problems">
          <p>The import file was rejected; nothing was changed:</p>
          <ul>
            {state.problems.map((problem, index) => (
              <li key={index}>{problem}</li>
            ))}
          </ul>
        </div>
      )}
      {state.error && (
        <p role="alert" data-testid="import-error">
          {state.error}
        </p>
      )}
    </form>
  );
}
