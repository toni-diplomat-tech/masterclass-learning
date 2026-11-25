# Course import file

One import file describes one Course: its Workshops and Lessons, and its Questions. The Host uploads it at `/host`; the builder script in `/tools` produces it from a course folder and the Question files. Real import files live in the git-ignored `content/` folder. [`testing-fundamentals.json`](testing-fundamentals.json) is a complete example; the backend tests import it, so it is always valid.

The app stores only the structure and a URL per Lesson, never the Lesson text.

## Format

A UTF-8 JSON object. Property names are case-sensitive and camelCase. Unknown properties are ignored.

```json
{
  "course": { "slug": "testing-fundamentals", "title": "Testing Fundamentals" },
  "workshops": [
    {
      "slug": "unit-tests",
      "title": "Unit tests",
      "number": 1,
      "lessons": [
        {
          "slug": "writing-your-first-test",
          "title": "Writing your first test",
          "number": 5,
          "url": "https://example.com/testing-fundamentals/01-unit-tests/05-writing-your-first-test"
        }
      ]
    }
  ],
  "questions": [
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
}
```

| Field | Rules |
|---|---|
| `course.slug` | Required, at most 100 characters. Identifies the Course: importing a file with the same slug updates that Course. |
| `course.title` | Required, at most 200 characters. |
| `workshops` | At least one. |
| `workshops[].slug` | Required, at most 100 characters, unique in the file. |
| `workshops[].title` | Required, at most 200 characters. |
| `workshops[].number` | Required integer, unique in the file. Workshops are shown in number order. |
| `workshops[].lessons` | At least one. |
| `lessons[].slug` | Required, at most 100 characters, unique in the **whole file** (Questions name their Lesson by slug alone). |
| `lessons[].title` | Required, at most 200 characters. The Recap shows it as "Unit tests · 05 Writing your first test". |
| `lessons[].number` | Required integer, unique within its Workshop. Lessons are shown in number order. |
| `lessons[].url` | Required, `http://` or `https://`, at most 2000 characters. Where the Recap sends the Participant to revisit the Lesson. |
| `questions[].id` | Required, at most 100 characters, unique in the file. Stable: keep it when you edit the Question, or its history is lost. |
| `questions[].lesson` | Required. The slug of a Lesson in this file. |
| `questions[].difficulty` | Required: `Easy`, `Medium` or `Hard`. |
| `questions[].text` | Required, at most 2000 characters. |
| `questions[].options` | 2 to 4 options, exactly one with `"correct": true`. Each `text` is required, at most 1000 characters. A missing `correct` means `false`. Options are numbered by their order in the file. |

Slugs and ids match case-insensitively. Text values are trimmed, and a blank value counts as missing.

## Importing and re-importing

An import is all or nothing: a file with any problem is rejected with the full list of problems, and nothing changes.

Re-importing is an upsert keyed on the Course slug, Workshop and Lesson slugs, Question ids and option positions:

- Titles, numbers, URLs, Question text, Difficulty, Lesson and options are updated in place. A Question keeps its identity, so past Answers stay attached to it.
- Re-importing an unchanged file changes nothing.
- A Question, Workshop, Lesson or option that is missing from the file is **retired**, not deleted: it is kept for past Answers but left out of the Course and of future Session selection. Putting it back in the file restores it.
- After an import, the back office shows the counts of Questions added, updated and retired.
