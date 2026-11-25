import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";

const numbered = /^(\d+)-(.+)$/;

/**
 * Builds one Course import file (format: docs/import-file/README.md) from a build config:
 * the course folder gives the Workshops and Lessons, the Question files give the Questions.
 * Returns the file and the problems found; the file is only usable when there are no problems.
 */
export function buildImportFile(configPath) {
  const problems = [];
  const config = JSON.parse(readFileSync(configPath, "utf8"));
  const courseFolder = resolve(dirname(configPath), config.courseFolder);
  const excluded = new Set(config.exclude ?? []);

  const workshops = numberedEntries(courseFolder, true)
    .filter((folder) => !excluded.has(folder.name) && !excluded.has(folder.slug))
    .map((folder) => ({
      slug: folder.slug,
      title: titleFromSlug(folder.slug),
      number: folder.number,
      lessons: lessonsIn(join(courseFolder, folder.name), folder, config, problems),
    }));

  const questionsFolder = resolve(dirname(configPath), config.questionsFolder);
  const lessonSlugs = new Set(workshops.flatMap((w) => w.lessons.map((l) => l.slug.toLowerCase())));
  const questions = [];
  for (const name of readdirSync(questionsFolder).filter((n) => n.endsWith(".json")).sort()) {
    for (const question of JSON.parse(readFileSync(join(questionsFolder, name), "utf8"))) {
      if (!lessonSlugs.has(String(question.lesson).toLowerCase())) {
        problems.push(`${name}: Question "${question.id}" names Lesson "${question.lesson}", which is not in the course folder.`);
      }
      questions.push(question);
    }
  }

  return { file: { course: config.course, workshops, questions }, problems };
}

function lessonsIn(path, workshopFolder, config, problems) {
  const lessons = [];
  for (const file of numberedEntries(path, false).filter((entry) => entry.name.endsWith(".md"))) {
    const stem = file.name.slice(0, -".md".length);
    if (stem.endsWith("-solution")) continue;
    lessons.push({
      slug: file.slug.slice(0, -".md".length),
      title: firstHeading(readFileSync(join(path, file.name), "utf8")),
      number: file.number,
      url: config.lessonUrl.replaceAll("{workshopFolder}", workshopFolder.name).replaceAll("{lessonFile}", stem),
    });
  }
  return lessons;
}

function numberedEntries(path, directories) {
  return readdirSync(path, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() === directories)
    .map((entry) => ({ entry, match: numbered.exec(entry.name) }))
    .filter(({ match }) => match)
    .map(({ entry, match }) => ({ name: entry.name, number: Number(match[1]), slug: match[2] }))
    .sort((a, b) => a.number - b.number);
}

function firstHeading(markdown) {
  let inFence = false;
  for (const line of markdown.split(/\r?\n/)) {
    if (/^\s*(```|~~~)/.test(line)) inFence = !inFence;
    else if (!inFence) {
      const heading = /^#{1,6}\s+(.+?)\s*#*\s*$/.exec(line);
      if (heading) return heading[1];
    }
  }
  return undefined;
}

function titleFromSlug(slug) {
  const words = slug.replaceAll("-", " ");
  return words.charAt(0).toUpperCase() + words.slice(1);
}
