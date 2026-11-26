import type { MasterclassStatus } from "./domain";
import { hostFetch } from "./host-session";

// Server-side only: the Host's Masterclass endpoints on the backend.

/**
 * Times are UTC ISO strings; joinCode is null until the Masterclass opens. nextWorkshopId is the Next
 * Workshop, the only one whose Session can start; null once every Workshop has had its Session.
 */
export type MasterclassOverview = {
  id: number;
  name: string;
  courseId: number;
  courseTitle: string;
  status: MasterclassStatus;
  joinCode: string | null;
  participants: number;
  createdAt: string;
  openedAt: string | null;
  closedAt: string | null;
  nextWorkshopId: number | null;
};

export type CreateResult = { masterclass: MasterclassOverview } | { problems: string[] };

/** Every Masterclass, newest first. */
export async function listMasterclasses(): Promise<MasterclassOverview[]> {
  const response = await hostFetch("/api/host/masterclasses");
  if (!response.ok) throw new Error(`Listing Masterclasses failed: HTTP ${response.status}`);
  return response.json();
}

/** One Masterclass, or null if there is none with this id. */
export async function getMasterclass(id: number): Promise<MasterclassOverview | null> {
  const response = await hostFetch(`/api/host/masterclasses/${id}`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Loading the Masterclass failed: HTTP ${response.status}`);
  return response.json();
}

/** Creates a Draft Masterclass for a Course. */
export async function createMasterclass(courseId: number, name: string): Promise<CreateResult> {
  const response = await hostFetch("/api/host/masterclasses", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ courseId, name }),
  });
  if (response.ok) return { masterclass: await response.json() };
  if (response.status === 400) {
    const problem: { problems?: string[] } = await response.json();
    if (problem.problems?.length) return { problems: problem.problems };
  }
  throw new Error(`Creating the Masterclass failed: HTTP ${response.status}`);
}

/**
 * Closes an Open Masterclass at the end of the meeting: no more joining or Sessions; Recaps stay readable.
 * Refused (with problems) for a Draft or while a Session runs.
 */
export async function closeMasterclass(id: number): Promise<{ problems: string[] }> {
  return changeMasterclass(id, "close", "Closing the Masterclass failed");
}

/** One Participant on the Masterclass leaderboard. Equal points share a rank (1, 1, 3). */
export type MasterclassLeaderboardEntry = { rank: number; participantId: number; nickname: string; points: number };

/** Points summed over all the Masterclass's Sessions, highest first. */
export type MasterclassLeaderboard = { masterclassId: number; entries: MasterclassLeaderboardEntry[] };

/** The Masterclass leaderboard, or null if there is no Masterclass with this id. */
export async function getMasterclassLeaderboard(id: number): Promise<MasterclassLeaderboard | null> {
  const response = await hostFetch(`/api/host/masterclasses/${id}/leaderboard`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Loading the Masterclass leaderboard failed: HTTP ${response.status}`);
  return response.json();
}

/**
 * Opens a Draft Masterclass, which gives it its Join code. Only one Masterclass can be Open at a time, so
 * it is refused (with problems, e.g. which Masterclass to close first) while another one is Open.
 */
export async function openMasterclass(id: number): Promise<{ problems: string[] }> {
  return changeMasterclass(id, "open", "Opening the Masterclass failed");
}

// POSTs a change to one Masterclass: no problems when it was made, the backend's problems when it was
// refused (409). Anything else throws, starting with `failure`.
async function changeMasterclass(id: number, change: "open" | "close", failure: string): Promise<{ problems: string[] }> {
  const response = await hostFetch(`/api/host/masterclasses/${id}/${change}`, { method: "POST" });
  if (response.ok) return { problems: [] };
  if (response.status === 409) {
    const problem: { problems?: string[] } = await response.json();
    if (problem.problems?.length) return { problems: problem.problems };
  }
  throw new Error(`${failure}: HTTP ${response.status}`);
}
