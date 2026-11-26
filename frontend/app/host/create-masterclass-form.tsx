"use client";

import { useActionState } from "react";
import { Button } from "@/components/button";
import { Notice } from "@/components/notice";
import { createMasterclassAction, type MasterclassActionState } from "./actions";
import styles from "./host.module.scss";

const initialState: MasterclassActionState = {};

export function CreateMasterclassForm({ courses }: { courses: { id: number; title: string }[] }) {
  const [state, formAction, pending] = useActionState(createMasterclassAction, initialState);

  return (
    <form action={formAction} className={styles.inlineForm}>
      <div className={styles.fields}>
        <label htmlFor="masterclass-name" className={styles.field}>
          Name
          <input id="masterclass-name" name="name" required maxLength={200} placeholder="Dev team 2026" />
        </label>
        <label htmlFor="masterclass-course" className={styles.field}>
          Course
          <select id="masterclass-course" name="courseId" required>
            {courses.map((course) => (
              <option key={course.id} value={course.id}>
                {course.title}
              </option>
            ))}
          </select>
        </label>
        <Button type="submit" loading={pending}>
          {pending ? "Creating…" : "Create Masterclass"}
        </Button>
      </div>
      {state.problems && (
        <Notice tone="error" role="alert" data-testid="create-masterclass-problems">
          <ul>
            {state.problems.map((problem, index) => (
              <li key={index}>{problem}</li>
            ))}
          </ul>
        </Notice>
      )}
      {state.error && (
        <Notice tone="error" role="alert" data-testid="create-masterclass-error">
          {state.error}
        </Notice>
      )}
    </form>
  );
}
