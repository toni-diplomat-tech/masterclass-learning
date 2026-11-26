using System.Text.Json.Nodes;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class SessionStartTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);

    private QuizEngine Engine => new(sqlServer.Connection, _time);

    [Fact]
    public async Task A_session_runs_the_requested_mix_from_easy_to_hard()
    {
        var meeting = await OpenMeetingAsync(easy: 4, medium: 4, hard: 4);

        var started = await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(2, 2, 1));

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

        var started = await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(mixEasy, mixMedium, mixHard));

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
        Assert.Empty((await Engine.ImportCourseAsync(file.ToJsonString())).Problems);

        var started = await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(3, 3, 1));

        Assert.Empty(started.Problems);
        var current = (await Engine.GetCourseAsync(meeting.CourseSlug))!.Workshops
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

    [Fact]
    public async Task The_first_question_starts_now_with_its_configured_time_limit()
    {
        var engine = new QuizEngine(sqlServer.Connection, new FakeTimeProvider(Now), settings: new QuizSettings
        {
            TimeLimitSeconds = new TimeLimitSeconds { Easy = 3, Medium = 4, Hard = 6.5 },
        });
        var meeting = await OpenMeetingAsync(easy: 1, medium: 1, hard: 1);

        var started = await engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));

        var first = started.Session!.FirstQuestion;
        Assert.Equal(
            (meeting.MasterclassId, started.Session.Id, "Unit tests", 1, 1, Difficulty.Easy, "Question easy-1?"),
            (first.MasterclassId, first.SessionId, first.WorkshopTitle, first.Number, first.QuestionCount, first.Difficulty, first.Text));
        Assert.Equal(started.Session.Questions[0].QuestionId, first.QuestionId);
        Assert.Equal(
            [new QuestionOption(1, "Right answer to easy-1"), new QuestionOption(2, "Wrong answer to easy-1"), new QuestionOption(3, "Another wrong answer to easy-1")],
            first.Options);
        Assert.Equal((Now.UtcDateTime, Now.UtcDateTime.AddSeconds(3)), (first.StartedAt, first.Deadline));
        Assert.Equal((DateTimeKind.Utc, DateTimeKind.Utc), (first.StartedAt.Kind, first.Deadline.Kind));
    }

    [Fact]
    public async Task A_hard_first_question_gets_the_hard_time_limit()
    {
        var engine = new QuizEngine(sqlServer.Connection, new FakeTimeProvider(Now), settings: new QuizSettings
        {
            TimeLimitSeconds = new TimeLimitSeconds { Easy = 3, Medium = 4, Hard = 6.5 },
        });
        var meeting = await OpenMeetingAsync(easy: 0, medium: 0, hard: 1);

        var started = await engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(0, 0, 1));

        Assert.Equal(Now.UtcDateTime.AddSeconds(6.5), started.Session!.FirstQuestion.Deadline);
    }

    [Fact]
    public async Task A_workshop_has_at_most_one_session_per_masterclass()
    {
        var meeting = await OpenMeetingAsync(easy: 4, medium: 0, hard: 0);
        await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));

        var again = await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));

        Assert.Null(again.Session);
        Assert.Equal(["This Workshop has already had its Session in this Masterclass."], again.Problems);
    }

    [Fact]
    public async Task A_workshop_whose_session_ended_cannot_start_another()
    {
        var meeting = await OpenMeetingAsync(easy: 4, medium: 0, hard: 0);
        await RunSessionAsync(meeting.MasterclassId, meeting.WorkshopId);

        var again = await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));

        Assert.Null(again.Session);
        Assert.Equal(["This Workshop has already had its Session in this Masterclass."], again.Problems);
    }

    [Fact]
    public async Task No_session_starts_once_every_workshop_has_had_its_session()
    {
        var meeting = await OpenMeetingAsync(easy: 4, medium: 0, hard: 0);
        var testDoublesId = await Engine.WorkshopIdAsync(meeting.MasterclassId, "test-doubles");
        await RunSessionAsync(meeting.MasterclassId, meeting.WorkshopId);
        await RunSessionAsync(meeting.MasterclassId, testDoublesId);

        var first = await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));
        var last = await Engine.StartSessionAsync(meeting.MasterclassId, testDoublesId, new DifficultyCounts(1, 0, 0));

        Assert.Equal(["This Workshop has already had its Session in this Masterclass."], first.Problems);
        Assert.Equal(["This Workshop has already had its Session in this Masterclass."], last.Problems);
    }

    [Fact]
    public async Task Only_one_session_runs_at_a_time()
    {
        var meeting = await OpenMeetingAsync(easy: 4, medium: 0, hard: 0);
        await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));
        var otherWorkshopId = (await Engine.ListCoursesAsync())
            .Single(c => c.Slug == meeting.CourseSlug).Workshops
            .Single(w => w.Slug != SessionFiles.WorkshopSlug).Id;

        var other = await Engine.StartSessionAsync(meeting.MasterclassId, otherWorkshopId, new DifficultyCounts(1, 0, 0));

        Assert.Equal(["Another Session is still running."], other.Problems);
    }

    [Fact]
    public async Task Only_the_next_workshop_can_start_its_session()
    {
        var meeting = await OpenMeetingAsync(easy: 4, medium: 0, hard: 0);

        var skipped = await Engine.StartSessionAsync(meeting.MasterclassId, await Engine.WorkshopIdAsync(meeting.MasterclassId, "test-doubles"), new DifficultyCounts(1, 0, 0));
        var next = await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));

        Assert.Null(skipped.Session);
        Assert.Equal(["Only the Next Workshop, 'Unit tests', can start its Session."], skipped.Problems);
        Assert.Empty(next.Problems); // the refused start left nothing behind
    }

    [Fact]
    public async Task A_workshop_retired_from_the_course_is_not_waited_for()
    {
        var file = SessionFiles.WithWorkshopQuestions(easy: 1, medium: 0, hard: 0);
        var meeting = await OpenMeetingAsync(file);
        // Re-import without the first Workshop: it is retired, with its Lessons and Questions.
        var unitTests = file["workshops"]!.AsArray()[0]!.AsObject();
        var lessons = unitTests["lessons"]!.AsArray().Select(l => (string)l!["slug"]!).ToHashSet();
        file["workshops"]!.AsArray().Remove(unitTests);
        foreach (var question in file["questions"]!.AsArray().Where(q => lessons.Contains((string)q!["lesson"]!)).ToList())
        {
            file["questions"]!.AsArray().Remove(question);
        }
        Assert.Empty((await Engine.ImportCourseAsync(file.ToJsonString())).Problems);

        var started = await Engine.StartSessionAsync(meeting.MasterclassId, await Engine.WorkshopIdAsync(meeting.MasterclassId, "test-doubles"), new DifficultyCounts(1, 0, 0));

        Assert.Empty(started.Problems);
    }

    [Fact]
    public async Task A_session_starts_only_in_an_open_masterclass()
    {
        var meeting = await OpenMeetingAsync(easy: 4, medium: 0, hard: 0);
        var draft = await Engine.CreateMasterclassAsync(
            (await Engine.GetMasterclassAsync(meeting.MasterclassId))!.CourseId, "Not opened yet");

        var started = await Engine.StartSessionAsync(draft.Masterclass!.Id, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));
        var unknown = await Engine.StartSessionAsync(-1, meeting.WorkshopId, new DifficultyCounts(1, 0, 0));

        Assert.Equal(["Sessions can only start in an Open Masterclass."], started.Problems);
        Assert.Equal(["Sessions can only start in an Open Masterclass."], unknown.Problems);
    }

    [Fact]
    public async Task A_session_is_for_a_workshop_of_the_masterclass_course()
    {
        var otherCourse = await OpenMeetingAsync(easy: 4, medium: 0, hard: 0);
        var meeting = await OpenMeetingAsync(easy: 4, medium: 0, hard: 0);

        var started = await Engine.StartSessionAsync(meeting.MasterclassId, otherCourse.WorkshopId, new DifficultyCounts(1, 0, 0));

        Assert.Equal(["There is no such Workshop in this Masterclass's Course."], started.Problems);
    }

    [Theory]
    [InlineData(3, 2, 2, "The Difficulty mix asks for 7 Questions but the Workshop has only 6.")]
    [InlineData(0, 0, 0, "The Difficulty mix needs at least one Question.")]
    [InlineData(1, -1, 0, "The Difficulty mix can't have negative counts.")]
    public async Task A_mix_must_ask_for_between_one_question_and_the_workshops_total(int easy, int medium, int hard, string problem)
    {
        var meeting = await OpenMeetingAsync(easy: 2, medium: 2, hard: 2);

        var started = await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(easy, medium, hard));
        var afterwards = await Engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(2, 2, 2));

        Assert.Equal([problem], started.Problems);
        Assert.Empty(afterwards.Problems); // the refused start left nothing behind
    }

    private Task<Meeting> OpenMeetingAsync(int easy, int medium, int hard) =>
        OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(easy, medium, hard));

    private async Task<Meeting> OpenMeetingAsync(JsonObject file)
    {
        var import = await Engine.ImportCourseAsync(file.ToJsonString());
        Assert.Empty(import.Problems);
        var workshopId = (await Engine.ListCoursesAsync())
            .Single(c => c.Id == import.Outcome!.CourseId).Workshops
            .Single(w => w.Slug == SessionFiles.WorkshopSlug).Id;
        var created = await Engine.CreateMasterclassAsync(import.Outcome!.CourseId, "Dev team 2026");
        await Engine.OpenAsOnlyOpenMasterclassAsync(created.Masterclass!.Id, sqlServer.Connection, _time);
        return new Meeting(created.Masterclass.Id, workshopId, import.Outcome.CourseSlug);
    }

    // Runs a one-Question Session of the Workshop to its end, letting the Question time out.
    private async Task RunSessionAsync(int masterclassId, int workshopId)
    {
        var started = await Engine.StartSessionAsync(masterclassId, workshopId, new DifficultyCounts(1, 0, 0));
        Assert.Empty(started.Problems);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.NotNull(await Engine.RevealQuestionAsync(started.Session!.Id));
        _time.Advance(TimeSpan.FromMinutes(1));
        await Engine.AdvanceSessionAsync(started.Session.Id);
        Assert.Equal(SessionStep.Ended, (await Engine.GetSessionProgressAsync(started.Session.Id))!.Step);
    }

    private sealed record Meeting(int MasterclassId, int WorkshopId, string CourseSlug);
}
