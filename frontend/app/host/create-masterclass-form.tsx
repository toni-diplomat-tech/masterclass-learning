"use client";

import { useActionState } from "react";
import { createMasterclassAction, type CreateMasterclassState } from "./actions";

const initialState: CreateMasterclassState = {};

export function CreateMasterclassForm({ courses }: { courses: { id: number; title: string }[] }) {
  const [state, formAction, pending] = useActionState(createMasterclassAction, initialState);

  return (
    <form action={formAction}>
      <p>
        <label htmlFor="masterclass-name">Name</label>{" "}
        <input id="masterclass-name" name="name" required maxLength={200} placeholder="Dev team 2026" />{" "}
        <label htmlFor="masterclass-course">Course</label>{" "}
        <select id="masterclass-course" name="courseId" required>
          {courses.map((course) => (
            <option key={course.id} value={course.id}>
              {course.title}
            </option>
          ))}
        </select>{" "}
        <button type="submit" disabled={pending}>
          {pending ? "Creating…" : "Create Masterclass"}
        </button>
      </p>
      {state.problems && (
        <ul role="alert" data-testid="create-masterclass-problems">
          {state.problems.map((problem, index) => (
            <li key={index}>{problem}</li>
          ))}
        </ul>
      )}
      {state.error && (
        <p role="alert" data-testid="create-masterclass-error">
          {state.error}
        </p>
      )}
    </form>
  );
}
