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

    [Theory]
    [InlineData("id", null, "Question #2 has no id.")]
    [InlineData("id", "unit-tests-001", "Question id 'unit-tests-001' is used more than once.")]
    [InlineData("lesson", null, "Question 'unit-tests-002' has no Lesson.")]
    [InlineData("difficulty", "Extreme", "Question 'unit-tests-002' has Difficulty 'Extreme'; it must be Easy, Medium or Hard.")]
    [InlineData("difficulty", null, "Question 'unit-tests-002' has no Difficulty; it must be Easy, Medium or Hard.")]
    [InlineData("text", "   ", "Question 'unit-tests-002' has no text.")]
    public async Task A_question_needs_a_unique_id_a_lesson_a_difficulty_and_text(string property, string? value, string problem)
    {
        _file.Question("unit-tests-002")[property] = value;

        await AssertRejectedAsync(problem);
    }

    [Fact]
    public async Task Every_option_needs_text()
    {
        _file.Question("unit-tests-002")["options"]![2]!["text"] = "";

        await AssertRejectedAsync("Option 3 of Question 'unit-tests-002' has no text.");
    }

    [Fact]
    public async Task The_course_needs_a_slug_and_a_title()
    {
        _file["course"] = new JsonObject { ["title"] = "" };

        await AssertRejectedAsync("The Course has no slug.", "The Course has no title.");
    }

    [Fact]
    public async Task The_file_needs_workshops()
    {
        _file["workshops"] = new JsonArray();
        _file["questions"] = new JsonArray();

        await AssertRejectedAsync("The file has no Workshops.");
    }

    [Theory]
    [InlineData("slug", null, "Workshop #2 has no slug.")]
    [InlineData("slug", "unit-tests", "Workshop slug 'unit-tests' is used more than once.")]
    [InlineData("title", null, "Workshop 'test-doubles' has no title.")]
    [InlineData("number", null, "Workshop 'test-doubles' has no number.")]
    [InlineData("number", 1, "Workshop number 1 is used more than once.")]
    public async Task A_workshop_needs_a_unique_slug_and_number_and_a_title(string property, object? value, string problem)
    {
        _file["workshops"]![1]![property] = JsonValue.Create(value);

        await AssertRejectedAsync(problem);
    }

    [Fact]
    public async Task A_workshop_needs_lessons()
    {
        _file["workshops"]![1]!["lessons"] = new JsonArray();
        _file["questions"]!.AsArray().Remove(_file.Question("test-doubles-001"));

        await AssertRejectedAsync("Workshop 'test-doubles' has no Lessons.");
    }

    [Theory]
    [InlineData("slug", null, "Lesson #1 of Workshop 'test-doubles' has no slug.")]
    [InlineData("slug", "why-we-test", "Lesson slug 'why-we-test' is used more than once.")]
    [InlineData("title", null, "Lesson 'stubs-and-mocks' has no title.")]
    [InlineData("number", null, "Lesson 'stubs-and-mocks' has no number.")]
    [InlineData("url", null, "Lesson 'stubs-and-mocks' has no URL.")]
    [InlineData("url", "example.com/stubs", "Lesson 'stubs-and-mocks' has URL 'example.com/stubs'; it must start with http:// or https://.")]
    public async Task A_lesson_needs_a_unique_slug_a_title_a_number_and_a_web_url(string property, object? value, string problem)
    {
        _file["workshops"]![1]!["lessons"]![0]![property] = JsonValue.Create(value);
        if (property == "slug")
        {
            _file["questions"]!.AsArray().Remove(_file.Question("test-doubles-001"));
        }

        await AssertRejectedAsync(problem);
    }

    [Fact]
    public async Task Lesson_numbers_are_unique_within_their_workshop()
    {
        _file["workshops"]![0]!["lessons"]![1]!["number"] = 1;
        _file["workshops"]![1]!["lessons"]![0]!["number"] = 5;

        await AssertRejectedAsync("Workshop 'unit-tests' has more than one Lesson number 1.");
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
