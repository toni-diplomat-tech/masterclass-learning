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
}
