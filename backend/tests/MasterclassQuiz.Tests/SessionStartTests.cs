using System.Text.Json.Nodes;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class SessionStartTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private readonly QuizEngine _engine = new(sqlServer.Connection, new FakeTimeProvider(Now));

    [Fact]
    public async Task A_session_runs_the_requested_mix_from_easy_to_hard()
    {
        var meeting = await OpenMeetingAsync(easy: 4, medium: 4, hard: 4);

        var started = await _engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(2, 2, 1));

        Assert.Empty(started.Problems);
        Assert.Equal(
            [Difficulty.Easy, Difficulty.Easy, Difficulty.Medium, Difficulty.Medium, Difficulty.Hard],
            started.Session!.Questions.Select(q => q.Difficulty));
    }

    // Workshop's Questions (Easy, Medium, Hard), mix asked for, Questions selected per Difficulty.
    [Theory]
    [InlineData(3, 3, 1, /* mix */ 0, 1, 3, /* selected */ 0, 3, 1)] // short Hard filled from Medium
    [InlineData(3, 1, 0, /* mix */ 1, 2, 0, /* selected */ 2, 1, 0)] // short Medium filled from Easy
    [InlineData(3, 1, 1, /* mix */ 0, 1, 3, /* selected */ 2, 1, 1)] // short Hard falls through Medium to Easy
    [InlineData(1, 2, 3, /* mix */ 4, 0, 0, /* selected */ 1, 2, 1)] // short Easy filled from Medium, then Hard
    [InlineData(0, 0, 2, /* mix */ 2, 0, 0, /* selected */ 0, 0, 2)] // short Easy filled from Hard when Medium has none
    public async Task A_difficulty_that_runs_short_is_filled_by_the_fallback_rule(
        int easy, int medium, int hard,
        int mixEasy, int mixMedium, int mixHard,
        int selectedEasy, int selectedMedium, int selectedHard)
    {
        var meeting = await OpenMeetingAsync(easy, medium, hard);

        var started = await _engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(mixEasy, mixMedium, mixHard));

        Assert.Empty(started.Problems);
        var questions = started.Session!.Questions;
        Assert.Equal(
            (selectedEasy, selectedMedium, selectedHard),
            (questions.Count(q => q.Difficulty == Difficulty.Easy), questions.Count(q => q.Difficulty == Difficulty.Medium), questions.Count(q => q.Difficulty == Difficulty.Hard)));
        Assert.Equal(questions.OrderBy(q => q.Difficulty).Select(q => q.Difficulty), questions.Select(q => q.Difficulty));
    }

    [Fact]
    public async Task A_session_takes_each_of_the_workshops_current_questions_at_most_once()
    {
        var file = SessionFiles.WithWorkshopQuestions(easy: 3, medium: 3, hard: 3);
        var meeting = await OpenMeetingAsync(file);
        // Re-import without two Questions: they are retired, and the Workshop has 7 current ones left.
        file["questions"]!.AsArray().Remove(file.Question("easy-2"));
        file["questions"]!.AsArray().Remove(file.Question("hard-3"));
        Assert.Empty((await _engine.ImportCourseAsync(file.ToJsonString())).Problems);

        var started = await _engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(3, 3, 1));

        Assert.Empty(started.Problems);
        var current = (await _engine.GetCourseAsync(meeting.CourseSlug))!.Workshops
            .Single(w => w.Id == meeting.WorkshopId).Lessons
            .SelectMany(l => l.Questions).Where(q => !q.Retired).Select(q => q.StableId);
        Assert.Equal(current.Order(), started.Session!.Questions.Select(q => q.StableId).Order());
    }

    [Fact]
    public async Task Questions_are_drawn_at_random_within_each_difficulty()
    {
        var picks = new HashSet<string>();
        for (var seed = 1; seed <= 5; seed++)
        {
            var engine = new QuizEngine(sqlServer.Connection, new FakeTimeProvider(Now), new Random(seed));
            var meeting = await OpenMeetingAsync(easy: 6, medium: 0, hard: 0);
            var started = await engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));
            picks.Add(started.Session!.Questions.Single().StableId);
        }

        // Always the same Question out of six in five draws would be very unlikely if drawn at random.
        Assert.True(picks.Count > 1, $"Every Session drew {string.Join(", ", picks)}.");
    }

    private Task<Meeting> OpenMeetingAsync(int easy, int medium, int hard) =>
        OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(easy, medium, hard));

    private async Task<Meeting> OpenMeetingAsync(JsonObject file)
    {
        var import = await _engine.ImportCourseAsync(file.ToJsonString());
        Assert.Empty(import.Problems);
        var workshopId = (await _engine.ListCoursesAsync())
            .Single(c => c.Id == import.Summary!.CourseId).Workshops
            .Single(w => w.Slug == SessionFiles.WorkshopSlug).Id;
        var created = await _engine.CreateMasterclassAsync(import.Summary!.CourseId, "Dev team 2026");
        await _engine.OpenMasterclassAsync(created.Masterclass!.Id);
        return new Meeting(created.Masterclass.Id, workshopId, import.Summary.CourseSlug);
    }

    private sealed record Meeting(int MasterclassId, int WorkshopId, string CourseSlug);
}
