using System.Text.Json;
using System.Text.Json.Nodes;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class CourseImportTests(SqlServerFixture sqlServer)
{
    private readonly QuizEngine _engine = new(
        sqlServer.Connection, new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Importing_the_example_file_creates_the_course_workshops_lessons_and_questions()
    {
        var result = await _engine.ImportCourseAsync(await File.ReadAllTextAsync(ImportFiles.ExamplePath));

        Assert.Empty(result.Problems);
        var course = await _engine.GetCourseAsync("testing-fundamentals");
        Assert.NotNull(course);
        Assert.Equal("Testing Fundamentals", course.Title);
        Assert.Equal(["unit-tests", "test-doubles"], course.Workshops.Select(w => w.Slug));
        var unitTests = course.Workshops[0];
        Assert.Equal(("Unit tests", 1), (unitTests.Title, unitTests.Number));
        var lesson = unitTests.Lessons[1];
        Assert.Equal(
            ("writing-your-first-test", "Writing your first test", 5, "https://example.com/testing-fundamentals/01-unit-tests/05-writing-your-first-test"),
            (lesson.Slug, lesson.Title, lesson.Number, lesson.Url));
        Assert.Equal(["unit-tests-002", "unit-tests-003"], lesson.Questions.Select(q => q.StableId));
        var hard = lesson.Questions[1];
        Assert.Equal(Difficulty.Hard, hard.Difficulty);
        Assert.Equal("A test passes whether or not the code under test is correct. What is it called?", hard.Text);
        Assert.Equal(
            [("A flaky test", false), ("A tautological test", true), ("An integration test", false), ("A smoke test", false)],
            hard.Options.Select(o => (o.Text, o.Correct)));
        Assert.False(hard.Retired);
    }

    [Fact]
    public async Task Reimporting_the_same_file_changes_nothing()
    {
        var file = ImportFiles.Example(ImportFiles.UniqueSlug());
        var first = await ImportAsync(file);
        var before = await GetCourseJsonAsync(file);

        var second = await ImportAsync(file);

        Assert.Equal((4, 0, 0), (first.Summary!.QuestionsAdded, first.Summary.QuestionsUpdated, first.Summary.QuestionsRetired));
        Assert.Equal((0, 0, 0), (second.Summary!.QuestionsAdded, second.Summary.QuestionsUpdated, second.Summary.QuestionsRetired));
        Assert.Equal(first.Summary.CourseId, second.Summary.CourseId);
        Assert.Equal(before, await GetCourseJsonAsync(file));
    }

    [Fact]
    public async Task Reimporting_an_edited_file_updates_text_in_place()
    {
        var file = ImportFiles.Example(ImportFiles.UniqueSlug());
        await ImportAsync(file);
        var questionId = (await GetQuestionAsync(file, "unit-tests-003")).Id;
        file["course"]!["title"] = "Testing Fundamentals, 2nd edition";
        var lesson = file["workshops"]![0]!["lessons"]![1]!;
        lesson["title"] = "Your first test";
        lesson["url"] = "https://example.com/v2/first-test";
        var question = file.Question("unit-tests-003");
        question["text"] = "A test that passes whatever the code does is called what?";
        question["difficulty"] = "Medium";
        question["options"]![0]!["text"] = "A FLAKY test";
        question["options"]!.AsArray().RemoveAt(3);

        var result = await ImportAsync(file);

        Assert.Equal((0, 1, 0), (result.Summary!.QuestionsAdded, result.Summary.QuestionsUpdated, result.Summary.QuestionsRetired));
        var course = (await _engine.GetCourseAsync(Slug(file)))!;
        Assert.Equal("Testing Fundamentals, 2nd edition", course.Title);
        var updatedLesson = course.Workshops[0].Lessons[1];
        Assert.Equal(("Your first test", "https://example.com/v2/first-test"), (updatedLesson.Title, updatedLesson.Url));
        var updated = updatedLesson.Questions.Single(q => q.StableId == "unit-tests-003");
        Assert.Equal(questionId, updated.Id);
        Assert.Equal("A test that passes whatever the code does is called what?", updated.Text);
        Assert.Equal(Difficulty.Medium, updated.Difficulty);
        Assert.Equal(
            [(1, "A FLAKY test", false), (2, "A tautological test", true), (3, "An integration test", false)],
            updated.Options.Select(o => (o.Position, o.Text, o.Correct)));
    }

    [Fact]
    public async Task Questions_removed_from_the_file_are_retired_and_come_back_when_restored()
    {
        var file = ImportFiles.Example(ImportFiles.UniqueSlug());
        await ImportAsync(file);
        var questionId = (await GetQuestionAsync(file, "test-doubles-001")).Id;
        var questions = file["questions"]!.AsArray();
        var removed = file.Question("test-doubles-001");
        questions.Remove(removed);

        var retiring = await ImportAsync(file);
        var retired = await GetQuestionAsync(file, "test-doubles-001");
        questions.Add(removed);
        var restoring = await ImportAsync(file);
        var restored = await GetQuestionAsync(file, "test-doubles-001");

        Assert.Equal((0, 0, 1), (retiring.Summary!.QuestionsAdded, retiring.Summary.QuestionsUpdated, retiring.Summary.QuestionsRetired));
        Assert.True(retired.Retired);
        Assert.Equal(questionId, retired.Id);
        Assert.Equal((0, 1, 0), (restoring.Summary!.QuestionsAdded, restoring.Summary.QuestionsUpdated, restoring.Summary.QuestionsRetired));
        Assert.False(restored.Retired);
        Assert.Equal(questionId, restored.Id);
    }

    [Fact]
    public async Task Courses_are_listed_with_their_workshops_and_current_question_counts_per_difficulty()
    {
        var file = ImportFiles.Example(ImportFiles.UniqueSlug());
        file["course"]!["title"] = "Testing Fundamentals (list test)";
        file.Question("unit-tests-002")["difficulty"] = "Easy";
        await ImportAsync(file);
        file["questions"]!.AsArray().Remove(file.Question("test-doubles-001"));
        await ImportAsync(file);

        var course = Assert.Single(await _engine.ListCoursesAsync(), c => c.Slug == Slug(file));

        Assert.Equal("Testing Fundamentals (list test)", course.Title);
        Assert.Equal(
            [("unit-tests", "Unit tests", 1, new DifficultyCounts(2, 0, 1)), ("test-doubles", "Test doubles", 2, new DifficultyCounts(0, 0, 0))],
            course.Workshops.Select(w => (w.Slug, w.Title, w.Number, w.Questions)));
    }

    [Fact]
    public async Task Workshops_and_lessons_removed_from_the_file_drop_out_of_the_course_and_come_back_when_restored()
    {
        var file = ImportFiles.Example(ImportFiles.UniqueSlug());
        await ImportAsync(file);
        file["workshops"]!.AsArray().RemoveAt(1);
        file["workshops"]![0]!["lessons"]!.AsArray().RemoveAt(0);
        var questions = file["questions"]!.AsArray();
        questions.Remove(file.Question("test-doubles-001"));
        questions.Remove(file.Question("unit-tests-001"));

        var removing = await ImportAsync(file);
        var reduced = (await _engine.GetCourseAsync(Slug(file)))!;
        await ImportAsync(ImportFiles.Example(Slug(file)));
        var restored = (await _engine.GetCourseAsync(Slug(file)))!;

        Assert.Equal(2, removing.Summary!.QuestionsRetired);
        Assert.Equal(["unit-tests"], reduced.Workshops.Select(w => w.Slug));
        Assert.Equal(["writing-your-first-test"], reduced.Workshops[0].Lessons.Select(l => l.Slug));
        Assert.Equal(["unit-tests", "test-doubles"], restored.Workshops.Select(w => w.Slug));
        Assert.Equal(["why-we-test", "writing-your-first-test"], restored.Workshops[0].Lessons.Select(l => l.Slug));
    }

    private Task<CourseImportResult> ImportAsync(JsonObject file) => _engine.ImportCourseAsync(file.ToJsonString());

    private static string Slug(JsonObject file) => (string)file["course"]!["slug"]!;

    private async Task<string> GetCourseJsonAsync(JsonObject file) =>
        JsonSerializer.Serialize(await _engine.GetCourseAsync(Slug(file)));

    private async Task<QuestionDetail> GetQuestionAsync(JsonObject file, string stableId) =>
        (await _engine.GetCourseAsync(Slug(file)))!.Workshops
            .SelectMany(w => w.Lessons).SelectMany(l => l.Questions)
            .Single(q => q.StableId == stableId);
}
