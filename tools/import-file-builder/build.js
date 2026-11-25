import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";

const numbered = /^(\d+)-(.+)$/;
const solutionSuffix = "-solution";

/**
 * Builds one Course import file (format: docs/import-file/README.md) from a build config.
 * The course folder gives the Workshops and Lessons, the Question files give the Questions.
 * Returns the file and the problems found; the file is only usable when there are no problems.
 * Everything else (difficulties, options, lengths...) is checked by the import itself.
 */
export function buildImportFile(configPath) {
  const problems = [];
  const config = JSON.parse(readFileSync(configPath, "utf8"));
  const courseFolder = resolve(dirname(configPath), config.courseFolder);
  const questionsFolder = resolve(dirname(configPath), config.questionsFolder);

  const workshops = readWorkshops(courseFolder, config, problems);
  const questions = readQuestions(questionsFolder, workshops, problems);

  return { file: { course: config.course, workshops, questions }, problems };
}

function readWorkshops(courseFolder, config, problems) {
  const folders = numberedEntries(courseFolder, true);
  const excluded = config.exclude ?? [];
  for (const name of excluded) {
    if (!folders.some((folder) => folder.name === name || folder.slug === name)) {
      problems.push(`Excluded folder "${name}" is not in the course folder.`);
    }
  }

  const lessonFiles = new Map();
  return folders
    .filter((folder) => !excluded.includes(folder.name) && !excluded.includes(folder.slug))
    .map((folder) => ({
      slug: folder.slug,
      title: titleFromSlug(folder.slug),
      number: folder.number,
      lessons: readLessons(join(courseFolder, folder.name), folder.name, config, lessonFiles, problems),
    }));
}

/** Numbered .md files are Lessons; a -solution.md file belongs to the Lesson just before it. */
function readLessons(folderPath, folderName, config, lessonFiles, problems) {
  const lessons = [];
  for (const file of numberedEntries(folderPath, false)) {
    if (!file.name.toLowerCase().endsWith(".md")) continue;
    const where = `${folderName}/${file.name}`;
    const stem = file.name.slice(0, -".md".length);
    if (stem.toLowerCase().endsWith(solutionSuffix)) {
      if (lessons.length === 0) problems.push(`${where}: a solution file needs a Lesson before it to merge into.`);
      continue;
    }

    const slug = file.slug.slice(0, -".md".length);
    const title = firstHeading(readFileSync(join(folderPath, file.name), "utf8"));
    if (title === undefined) problems.push(`${where}: no heading to use as the Lesson title.`);
    const earlier = lessonFiles.get(slug.toLowerCase());
    if (earlier) {
      problems.push(`${where}: Lesson slug "${slug}" is already used by ${earlier}. Rename one of the files.`);
    } else {
      lessonFiles.set(slug.toLowerCase(), where);
    }

    lessons.push({
      slug,
      title,
      number: file.number,
      url: config.lessonUrl
        .replaceAll("{workshopFolder}", encodeURIComponent(folderName))
        .replaceAll("{lessonFile}", encodeURIComponent(stem)),
    });
  }
  return lessons;
}

/** Every .json file in the Question folder holds an array of Questions in the import file's format. */
function readQuestions(questionsFolder, workshops, problems) {
  const lessonSlugs = new Set(workshops.flatMap((w) => w.lessons.map((l) => l.slug.toLowerCase())));
  const questions = [];
  const files = readdirSync(questionsFolder)
    .filter((name) => name.toLowerCase().endsWith(".json"))
    .sort();
  for (const name of files) {
    let fileQuestions;
    try {
      fileQuestions = JSON.parse(stripBom(readFileSync(join(questionsFolder, name), "utf8")));
    } catch (error) {
      problems.push(`${name}: not valid JSON (${error.message}).`);
      continue;
    }
    if (!Array.isArray(fileQuestions)) {
      problems.push(`${name}: must be a JSON array of Questions.`);
      continue;
    }
    for (const question of fileQuestions) {
      if (!lessonSlugs.has(String(question?.lesson).toLowerCase())) {
        problems.push(`${name}: Question "${question?.id}" names Lesson "${question?.lesson}", which is not in the course folder.`);
      }
      questions.push(question);
    }
  }
  return questions;
}

function numberedEntries(path, directories) {
  return readdirSync(path, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() === directories)
    .map((entry) => ({ entry, match: numbered.exec(entry.name) }))
    .filter(({ match }) => match)
    .map(({ entry, match }) => ({ name: entry.name, number: Number(match[1]), slug: match[2] }))
    .sort((a, b) => a.number - b.number || a.name.localeCompare(b.name));
}

/** The first Markdown heading of any level, skipping front matter and fenced code blocks. */
function firstHeading(markdown) {
  const lines = stripBom(markdown).split(/\r?\n/);
  let start = 0;
  if (lines[0]?.trim() === "---") {
    const end = lines.indexOf("---", 1);
    if (end > 0) start = end + 1;
  }
  let fence = null;
  for (const line of lines.slice(start)) {
    const marker = /^\s*(`{3,}|~{3,})/.exec(line)?.[1];
    if (fence) {
      if (marker && marker[0] === fence[0] && marker.length >= fence.length) fence = null;
    } else if (marker) {
      fence = marker;
    } else {
      const heading = /^ {0,3}#{1,6}\s+(.+?)(\s+#+)?\s*$/.exec(line);
      if (heading) return heading[1];
    }
  }
  return undefined;
}

function titleFromSlug(slug) {
  const words = slug.replaceAll("-", " ");
  return words.charAt(0).toUpperCase() + words.slice(1);
}

function stripBom(text) {
  return text.charCodeAt(0) === 0xfeff ? text.slice(1) : text;
}
