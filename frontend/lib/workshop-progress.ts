import type { WorkshopOverview } from "./courses";

/**
 * A Workshop's place in a Masterclass: `done` (before the Next Workshop, its Session has run), `next` (the
 * Next Workshop, the only one whose Session can start) or `locked` (after it).
 */
export type WorkshopStep = "done" | "next" | "locked";

/**
 * The Course's Workshops in Course order, each with its step, given the Next Workshop the backend named
 * (null: every Workshop has had its Session, so all are done).
 */
export function workshopProgress(
  workshops: WorkshopOverview[],
  nextWorkshopId: number | null,
): { workshop: WorkshopOverview; step: WorkshopStep }[] {
  const inCourseOrder = workshops.toSorted((a, b) => a.number - b.number);
  const nextIndex = inCourseOrder.findIndex((w) => w.id === nextWorkshopId);
  return inCourseOrder.map((workshop, index) => ({
    workshop,
    step: nextIndex === -1 || index < nextIndex ? "done" : index === nextIndex ? "next" : "locked",
  }));
}
