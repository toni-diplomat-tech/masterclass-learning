import type { WorkshopOverview } from "./courses";

/**
 * A Workshop's place in a Masterclass: `done` (it has had its Session there), `next` (the Next Workshop,
 * the only one whose Session can start) or `locked` (neither: its turn comes after the Next Workshop's).
 */
export type WorkshopStep = "done" | "next" | "locked";

/**
 * The Course's Workshops in Course order (number, then id, as the backend orders them), each with its
 * step, given the Next Workshop the backend named (null once every current Workshop has had its Session)
 * and the Workshops that have had their Session in the Masterclass. A Workshop that had its Session stays
 * done even if a re-import has since moved it after the Next Workshop.
 */
export function workshopProgress(
  workshops: WorkshopOverview[],
  nextWorkshopId: number | null,
  workshopIdsWithSession: number[],
): { workshop: WorkshopOverview; step: WorkshopStep }[] {
  const hadSession = new Set(workshopIdsWithSession);
  return workshops
    .toSorted((a, b) => a.number - b.number || a.id - b.id)
    .map((workshop) => ({
      workshop,
      step: hadSession.has(workshop.id) ? "done" : workshop.id === nextWorkshopId ? "next" : "locked",
    }));
}
