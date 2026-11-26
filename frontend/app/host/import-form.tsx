"use client";

import { useActionState } from "react";
import { Button } from "@/components/button";
import { Notice } from "@/components/notice";
import { importCourseFile, type ImportState } from "./actions";
import styles from "./host.module.scss";

const initialState: ImportState = {};

export function ImportForm() {
  const [state, formAction, pending] = useActionState(importCourseFile, initialState);

  return (
    <form action={formAction} className={styles.inlineForm}>
      <div className={styles.fields}>
        <label htmlFor="import-file" className={styles.field}>
          Course import file
          <input id="import-file" name="file" type="file" accept=".json,application/json" required />
        </label>
        <Button type="submit" variant="secondary" loading={pending}>
          {pending ? "Importing…" : "Import"}
        </Button>
      </div>
      {state.outcome && (
        <Notice tone="success" role="status" data-testid="import-outcome">
          Imported {state.outcome.courseTitle}: {state.outcome.workshops} Workshops, {state.outcome.lessons} Lessons,{" "}
          {state.outcome.questions} Questions ({state.outcome.questionsAdded} added, {state.outcome.questionsUpdated}{" "}
          updated, {state.outcome.questionsRetired} retired).
        </Notice>
      )}
      {state.problems && (
        <Notice tone="error" role="alert" data-testid="import-problems">
          <p>The import file was rejected; nothing was changed:</p>
          <ul>
            {state.problems.map((problem, index) => (
              <li key={index}>{problem}</li>
            ))}
          </ul>
        </Notice>
      )}
      {state.error && (
        <Notice tone="error" role="alert" data-testid="import-error">
          {state.error}
        </Notice>
      )}
    </form>
  );
}
