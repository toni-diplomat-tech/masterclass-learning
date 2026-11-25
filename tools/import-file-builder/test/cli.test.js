import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

const script = fileURLToPath(new URL("../build-import-file.js", import.meta.url));
const documentedExample = JSON.parse(
  readFileSync(new URL("../../../docs/import-file/testing-fundamentals.json", import.meta.url), "utf8"),
);

function copyOfFixture() {
  const root = mkdtempSync(join(tmpdir(), "import-file-builder-cli-"));
  cpSync(fileURLToPath(new URL("fixtures/testing-fundamentals", import.meta.url)), root, { recursive: true });
  return root;
}

const run = (...args) => spawnSync(process.execPath, [script, ...args], { encoding: "utf8" });

test("writes the import file next to the build config, named after the Course", () => {
  const root = copyOfFixture();

  const result = run(join(root, "course.json"));

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(readFileSync(join(root, "testing-fundamentals.import.json"), "utf8")), documentedExample);
  assert.match(result.stdout, /2 Workshops, 3 Lessons, 4 Questions/);
});

test("writes the import file where --out says", () => {
  const root = copyOfFixture();
  const out = join(root, "elsewhere.json");

  const result = run(join(root, "course.json"), "--out", out);

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(readFileSync(out, "utf8")), documentedExample);
});

test("lists the problems, fails and writes nothing when there are problems", () => {
  const root = copyOfFixture();
  writeFileSync(join(root, "questions", "03-extra.json"), JSON.stringify([{ id: "x-001", lesson: "no-such-lesson" }]));

  const result = run(join(root, "course.json"));

  assert.equal(result.status, 1);
  assert.match(result.stderr, /03-extra\.json: Question "x-001" names Lesson "no-such-lesson"/);
  assert.equal(existsSync(join(root, "testing-fundamentals.import.json")), false);
});

test("explains its usage when the build config is missing", () => {
  const result = run();

  assert.equal(result.status, 2);
  assert.match(result.stderr, /Usage:/);
});
