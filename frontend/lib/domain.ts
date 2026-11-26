// Runtime-neutral domain types, shared by server-side code (lib/courses.ts, lib/results.ts, …) and the
// browser (lib/live-masterclass). Types only: nothing here may touch the server or the browser.

export type MasterclassStatus = "Draft" | "Open" | "Closed";

export type Difficulty = "Easy" | "Medium" | "Hard";

/** A number per Difficulty: a Workshop's Question counts, or the mix the Host sets for a Session. */
export type DifficultyCounts = { easy: number; medium: number; hard: number };
