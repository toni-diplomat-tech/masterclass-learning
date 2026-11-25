import { hostFetch } from "./host-session";

// Server-side only: the Host's Course endpoints on the backend.

export type DifficultyCounts = { easy: number; medium: number; hard: number };

export type WorkshopOverview = { id: number; slug: string; title: string; number: number; questions: DifficultyCounts };

export type CourseOverview = { id: number; slug: string; title: string; workshops: WorkshopOverview[] };

export type ImportSummary = {
  courseSlug: string;
  courseTitle: string;
  workshops: number;
  lessons: number;
  questions: number;
  questionsAdded: number;
  questionsUpdated: number;
  questionsRetired: number;
};

export type ImportResult = { summary: ImportSummary } | { problems: string[] };

const IMPORT_TIMEOUT_MS = 30000;

/** Every Course with its Workshops and their Question counts per Difficulty (retired Questions excluded). */
export async function listCourses(): Promise<CourseOverview[]> {
  const response = await hostFetch("/api/host/courses");
  if (!response.ok) throw new Error(`Listing Courses failed: HTTP ${response.status}`);
  return response.json();
}

/** Uploads an import file's contents; the backend stores all of it or none of it. */
export async function importCourse(fileContents: string): Promise<ImportResult> {
  const response = await hostFetch("/api/host/courses/import", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: fileContents,
    signal: AbortSignal.timeout(IMPORT_TIMEOUT_MS),
  });
  if (response.ok) return { summary: await response.json() };
  if (response.status === 400) {
    const problem: { problems?: string[] } = await response.json();
    if (problem.problems?.length) return { problems: problem.problems };
  }
  throw new Error(`Importing the Course failed: HTTP ${response.status}`);
}
