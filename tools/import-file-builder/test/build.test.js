import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
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
