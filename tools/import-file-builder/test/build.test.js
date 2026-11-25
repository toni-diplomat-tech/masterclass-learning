import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import { buildImportFile } from "../build.js";

const fixture = fileURLToPath(new URL("fixtures/testing-fundamentals/course.json", import.meta.url));
const documentedExample = JSON.parse(
  readFileSync(new URL("../../../docs/import-file/testing-fundamentals.json", import.meta.url), "utf8"),
);

test("numbered folders become Workshops and numbered .md files become Lessons, in order, with their URLs", () => {
  const { file, problems } = buildImportFile(fixture);

  assert.deepEqual(problems, []);
  assert.deepEqual(file.course, documentedExample.course);
  assert.deepEqual(file.workshops, documentedExample.workshops);
});

test("Questions come from every Question file, in file name order, so the output is the documented example the backend imports", () => {
  const { file, problems } = buildImportFile(fixture);

  assert.deepEqual(problems, []);
  assert.deepEqual(file, documentedExample);
});

test("a Question naming an unknown Lesson slug is reported", () => {
  const config = buildConfig({
    "course/01-unit-tests/01-why-we-test.md": "# Why we test\n",
    "questions/unit-tests.json": JSON.stringify([
      question("unit-tests-001", "why-we-test"),
      question("unit-tests-002", "why-we-tset"),
    ]),
  });

  const { problems } = buildImportFile(config);

  assert.deepEqual(problems, ['unit-tests.json: Question "unit-tests-002" names Lesson "why-we-tset", which is not in the course folder.']);
});

test("a Lesson without a heading is reported, since it has no title", () => {
  const config = buildConfig({ "course/01-unit-tests/01-why-we-test.md": "Tests catch regressions.\n" });

  const { problems } = buildImportFile(config);

  assert.deepEqual(problems, ["01-unit-tests/01-why-we-test.md: no heading to use as the Lesson title."]);
});

test("two Lessons with the same slug are reported, since Questions name a Lesson by slug alone", () => {
  const config = buildConfig({
    "course/01-unit-tests/03-q-and-a.md": "# Q&A\n",
    "course/02-test-doubles/04-q-and-a.md": "# Q&A\n",
  });

  const { problems } = buildImportFile(config);

  assert.deepEqual(problems, [
    '02-test-doubles/04-q-and-a.md: Lesson slug "q-and-a" is already used by 01-unit-tests/03-q-and-a.md. Rename one of the files.',
  ]);
});

test("a solution file with no Lesson before it is reported", () => {
  const config = buildConfig({ "course/01-unit-tests/01-why-we-test-solution.md": "# Why we test: solution\n" });

  const { problems } = buildImportFile(config);

  assert.deepEqual(problems, ["01-unit-tests/01-why-we-test-solution.md: a solution file needs a Lesson before it to merge into."]);
});

test("an excluded folder that is not in the course folder is reported, since it is probably a typo", () => {
  const config = buildConfig({ "course/01-unit-tests/01-why-we-test.md": "# Why we test\n" }, { exclude: ["03-ofice-hours"] });

  const { problems } = buildImportFile(config);

  assert.deepEqual(problems, ['Excluded folder "03-ofice-hours" is not in the course folder.']);
});

test("a Question file that is not a JSON array is reported", () => {
  const config = buildConfig({
    "course/01-unit-tests/01-why-we-test.md": "# Why we test\n",
    "questions/broken.json": '[{ "id": "unit-tests-001", }]',
    "questions/object.json": JSON.stringify(question("unit-tests-002", "why-we-test")),
  });

  const { problems } = buildImportFile(config);

  assert.equal(problems.length, 2);
  assert.match(problems[0], /^broken\.json: not valid JSON/);
  assert.equal(problems[1], "object.json: must be a JSON array of Questions.");
});

/** Writes a course folder and Question files into a temporary folder; returns the build config's path. */
function buildConfig(files, config = {}) {
  const root = mkdtempSync(join(tmpdir(), "import-file-builder-"));
  for (const [path, contents] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), contents);
  }
  mkdirSync(join(root, "questions"), { recursive: true });
  const configPath = join(root, "course.json");
  writeFileSync(
    configPath,
    JSON.stringify({
      course: { slug: "testing-fundamentals", title: "Testing Fundamentals" },
      courseFolder: "course",
      questionsFolder: "questions",
      lessonUrl: "https://example.com/{workshopFolder}/{lessonFile}",
      ...config,
    }),
  );
  return configPath;
}

function question(id, lesson) {
  return {
    id,
    lesson,
    difficulty: "Easy",
    text: "What is the main benefit of an automated test suite?",
    options: [
      { text: "It catches regressions before users do", correct: true },
      { text: "It makes the code run faster", correct: false },
    ],
  };
}
