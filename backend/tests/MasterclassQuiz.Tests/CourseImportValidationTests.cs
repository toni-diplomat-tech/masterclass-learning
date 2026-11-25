using System.Text.Json.Nodes;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>An invalid import file is rejected as a whole, with a message per problem, and stores nothing.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class CourseImportValidationTests(SqlServerFixture sqlServer)
{
    private readonly QuizEngine _engine = new(
        sqlServer.Connection, new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero)));

    private readonly JsonObject _file = ImportFiles.Example(ImportFiles.UniqueSlug());

    [Theory]
    [InlineData(new[] { false, false, false }, "Question 'test-doubles-001' has no correct option; exactly one must be correct.")]
    [InlineData(new[] { true, true, false }, "Question 'test-doubles-001' has 2 correct options; exactly one must be correct.")]
    public async Task A_question_needs_exactly_one_correct_option(bool[] correct, string problem)
    {
        var options = _file.Question("test-doubles-001")["options"]!.AsArray();
        for (var i = 0; i < correct.Length; i++)
        {
            options[i]!["correct"] = correct[i];
        }

        await AssertRejectedAsync(problem);
    }

    [Theory]
    [InlineData(1, "Question 'unit-tests-001' has 1 option; it needs 2 to 4.")]
    [InlineData(5, "Question 'unit-tests-001' has 5 options; it needs 2 to 4.")]
    public async Task A_question_needs_two_to_four_options(int count, string problem)
    {
        var options = new JsonArray(new JsonObject { ["text"] = "Right", ["correct"] = true });
        for (var i = 1; i < count; i++)
        {
            options.Add(new JsonObject { ["text"] = $"Wrong {i}", ["correct"] = false });
        }
        _file.Question("unit-tests-001")["options"] = options;

        await AssertRejectedAsync(problem);
    }

    [Fact]
    public async Task A_question_must_name_a_lesson_in_the_file()
    {
        _file.Question("unit-tests-002")["lesson"] = "writing-your-second-test";

        await AssertRejectedAsync("Question 'unit-tests-002' names Lesson 'writing-your-second-test', which is not in the file.");
    }

    [Fact]
    public async Task Every_problem_is_reported_in_file_order()
    {
        _file.Question("unit-tests-003")["lesson"] = "no-such-lesson";
        _file.Question("unit-tests-001")["options"]!.AsArray().RemoveAt(1);

        await AssertRejectedAsync(
            "Question 'unit-tests-001' has 1 option; it needs 2 to 4.",
            "Question 'unit-tests-003' names Lesson 'no-such-lesson', which is not in the file.");
    }

    private async Task AssertRejectedAsync(params string[] problems)
    {
        var result = await _engine.ImportCourseAsync(_file.ToJsonString());

        Assert.Equal(problems, result.Problems);
        Assert.Null(result.Summary);
        Assert.Null(await _engine.GetCourseAsync((string)_file["course"]!["slug"]!));
    }
}
