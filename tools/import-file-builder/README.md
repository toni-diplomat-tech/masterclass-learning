# Import-file builder

Builds one Course import file (format: [`docs/import-file/`](../../docs/import-file/README.md)) from a course folder and the Question files. The Host then uploads it at `/host`. Needs Node 22 or later; there is nothing to install.

Keep the build config, the Question files and the generated import file in the git-ignored `content/` folder. Never commit real course material.

## Usage

From the repo root:

```sh
node tools/import-file-builder/build-import-file.js content/testing-fundamentals/course.json
```

This writes `content/testing-fundamentals/testing-fundamentals.import.json` (next to the build config, named after the Course slug). Pass `--out <path>` to write it somewhere else.

If anything is wrong, the script lists every problem, writes nothing and exits with code 1. It checks the course structure and that every Question names a Lesson in it. The import itself checks the rest (Difficulty, options, lengths...), so a file the script accepts can still be rejected at upload with a list of problems.

## Build config

```json
{
  "course": { "slug": "testing-fundamentals", "title": "Testing Fundamentals" },
  "courseFolder": "../../course-material/testing-fundamentals",
  "questionsFolder": "questions",
  "lessonUrl": "https://example.com/testing-fundamentals/{workshopFolder}/{lessonFile}",
  "exclude": ["03-office-hours"],
  "workshopTitles": { "02-test-doubles": "Test doubles: stubs, mocks and fakes" }
}
```

| Field | Meaning |
|---|---|
| `course` | The Course's slug and title, copied into the import file. Keep the slug when re-importing, or you get a second Course. |
| `courseFolder` | The course folder, relative to the build config (an absolute path also works). |
| `questionsFolder` | The folder of Question files, relative to the build config. |
| `lessonUrl` | Each Lesson's URL, where the Recap sends a Participant. `{workshopFolder}` and `{lessonFile}` are replaced by the Workshop's folder name and the Lesson's file name without `.md`, e.g. `01-unit-tests` and `05-writing-your-first-test`. |
| `exclude` | Optional. Folders to skip, by folder name (`03-office-hours`) or slug (`office-hours`). |
| `workshopTitles` | Optional. Workshop titles by folder name or slug, for Workshops whose default title is not good enough. |

## Course folder

```
testing-fundamentals/
  README.md                                  not numbered: ignored
  01-unit-tests/                             Workshop 1, slug "unit-tests", title "Unit tests"
    01-why-we-test.md                        Lesson 1, slug "why-we-test"
    05-writing-your-first-test.md            Lesson 5, slug "writing-your-first-test"
    06-writing-your-first-test-solution.md   merged into Lesson 5
  02-test-doubles/
    01-stubs-and-mocks.md
  03-office-hours/                           skipped when excluded
```

- Each numbered folder (`NN-slug`) is a Workshop. Its number and slug come from the folder name, and its title is the slug with spaces and a capital first letter, unless `workshopTitles` names it.
- Each numbered `.md` file in it (`NN-slug.md`) is a Lesson. Its number and slug come from the file name. Its title is the file's first Markdown heading, at any level, skipping front matter and code blocks.
- A file whose name ends in `-solution.md` is not a Lesson. It belongs to the Lesson just before it, so Questions about it name that Lesson.
- Lesson slugs must be unique across the whole Course, because a Question names its Lesson by slug alone.

## Question files

Every `.json` file in `questionsFolder` is a JSON array of Questions, in the import file's format. Files are read in file name order, so number them (`01-unit-tests.json`, `02-test-doubles.json`) to keep Questions in Workshop order.

```json
[
  {
    "id": "unit-tests-002",
    "lesson": "writing-your-first-test",
    "difficulty": "Medium",
    "text": "Which three steps make up the Arrange-Act-Assert pattern?",
    "options": [
      { "text": "Set up, run the code under test, check the result", "correct": true },
      { "text": "Compile, deploy, monitor", "correct": false },
      { "text": "Write, review, merge", "correct": false }
    ]
  }
]
```

Keep each Question's `id` when you edit it, or its history is lost.

## Tests

```sh
cd tools/import-file-builder
npm test
```

The tests build the made-up "Testing Fundamentals" Course in `test/fixtures/` and check that the result equals [`docs/import-file/testing-fundamentals.json`](../../docs/import-file/testing-fundamentals.json), which the backend tests import. If you change one, change the other.
