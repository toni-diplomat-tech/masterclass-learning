#!/usr/bin/env node
// Builds a Course import file from a build config. Usage: see tools/import-file-builder/README.md.
import { writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { parseArgs } from "node:util";
import { buildImportFile } from "./build.js";

const usage = "Usage: node tools/import-file-builder/build-import-file.js <build config .json> [--out <import file>]";

let args;
try {
  args = parseArgs({ allowPositionals: true, options: { out: { type: "string" } } });
} catch (error) {
  fail(2, `${error.message}\n${usage}`);
}
if (args.positionals.length !== 1) fail(2, usage);

const configPath = resolve(args.positionals[0]);
let result;
try {
  result = buildImportFile(configPath);
} catch (error) {
  fail(1, `Could not build the import file: ${error.message}`);
}

const { file, problems } = result;
if (problems.length > 0) {
  fail(1, [`Found ${problems.length} problem(s); no import file written:`, ...problems.map((p) => `- ${p}`)].join("\n"));
}

const out = resolve(args.values.out ?? join(dirname(configPath), `${file.course?.slug ?? "course"}.import.json`));
writeFileSync(out, `${JSON.stringify(file, null, 2)}\n`);
const lessons = file.workshops.reduce((sum, workshop) => sum + workshop.lessons.length, 0);
console.log(`Wrote ${out}: ${file.workshops.length} Workshops, ${lessons} Lessons, ${file.questions.length} Questions.`);

function fail(exitCode, message) {
  console.error(message);
  process.exit(exitCode);
}
