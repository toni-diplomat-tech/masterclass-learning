using System.Text.Json.Nodes;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>
/// A Participant's Recap: by Workshop in Course order, a Perfect score, the missed Questions by Lesson, or
/// Not attended; only for Sessions that have ended.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class RecapTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    // Easy Questions have 3 seconds to answer (3.5 s with grace); no pause after a reveal.
    private readonly FakeTimeProvider _time = new(Now);
    private QuizEngine Engine => new(sqlServer.Connection, _time, settings: new QuizSettings
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3 },
        RevealPauseSeconds = 0,
    });

    [Fact]
    public async Task Answering_every_question_correct_earns_a_perfect_score()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(2, 0, 0));
        var participant = await meeting.JoinAsync();

        await meeting.RunSessionAsync("unit-tests", new() { [participant] = _ => 1 });

        var recap = await RecapOfAsync(participant);
        var workshop = Assert.Single(recap.Workshops);
        Assert.Equal((1, "Unit tests", RecapOutcome.PerfectScore), (workshop.Number, workshop.Title, workshop.Outcome));
        Assert.Empty(workshop.Lessons);
    }

    [Fact]
    public async Task Missed_questions_are_grouped_by_lesson_with_the_answer_given_and_the_correct_option()
    {
        var file = SessionFiles.WithWorkshopQuestions(3, 0, 0);
        file.Question("easy-2")["lesson"] = "writing-your-first-test";
        file.Question("easy-3")["lesson"] = "writing-your-first-test";
        var meeting = await OpenMeetingAsync(file);
        var participant = await meeting.JoinAsync();

        await meeting.RunSessionAsync("unit-tests", new()
        {
            [participant] = id => id switch { "easy-1" => 2, "easy-2" => 1, _ => null },
        });

        var workshop = Assert.Single((await RecapOfAsync(participant)).Workshops);
        Assert.Equal(RecapOutcome.MissedQuestions, workshop.Outcome);
        Assert.Equal(2, workshop.Lessons.Count);
        var (first, fifth) = (workshop.Lessons[0], workshop.Lessons[1]);
        Assert.Equal(
            (1, "Why we test", "https://example.com/testing-fundamentals/01-unit-tests/01-why-we-test"),
            (first.Number, first.Title, first.Url));
        Assert.Equal([new MissedQuestion("Question easy-1?", "Wrong answer to easy-1", false, "Right answer to easy-1")], first.Questions);
        Assert.Equal(
            (5, "Writing your first test", "https://example.com/testing-fundamentals/01-unit-tests/05-writing-your-first-test"),
            (fifth.Number, fifth.Title, fifth.Url));
        Assert.Equal([new MissedQuestion("Question easy-3?", null, true, "Right answer to easy-3")], fifth.Questions);
    }

    [Fact]
    public async Task A_missed_question_keeps_the_options_it_ran_with_after_a_re_import_changes_them()
    {
        var file = SessionFiles.WithWorkshopQuestions(1, 0, 0);
        var meeting = await OpenMeetingAsync(file);
        var participant = await meeting.JoinAsync();
        await meeting.RunSessionAsync("unit-tests", new() { [participant] = _ => 2 });

        // The correct option moves to where the Participant's wrong one was, and option 3 is retired.
        file.Question("easy-1")["options"] = new JsonArray(
            new JsonObject { ["text"] = "Wrong answer to easy-1" },
            new JsonObject { ["text"] = "Right answer to easy-1", ["correct"] = true });
        Assert.Empty((await Engine.ImportCourseAsync(file.ToJsonString())).Problems);

        var lesson = Assert.Single(Assert.Single((await RecapOfAsync(participant)).Workshops).Lessons);
        Assert.Equal([new MissedQuestion("Question easy-1?", "Wrong answer to easy-1", false, "Right answer to easy-1")], lesson.Questions);
    }

    [Fact]
    public async Task Joining_after_a_session_ended_lists_all_its_workshops_lessons_as_not_attended()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(1, 0, 0));
        var early = await meeting.JoinAsync();
        await meeting.RunSessionAsync("unit-tests", new() { [early] = _ => 1 });
        var late = await meeting.JoinAsync();

        await meeting.RunSessionAsync("test-doubles", new() { [late] = _ => 1 });

        var recap = await RecapOfAsync(late);
        Assert.Equal(
            [("Unit tests", RecapOutcome.NotAttended), ("Test doubles", RecapOutcome.PerfectScore)],
            recap.Workshops.Select(w => (w.Title, w.Outcome)));
        Assert.Equal(
            [
                new RecapLesson(1, "Why we test", "https://example.com/testing-fundamentals/01-unit-tests/01-why-we-test", []),
                new RecapLesson(5, "Writing your first test", "https://example.com/testing-fundamentals/01-unit-tests/05-writing-your-first-test", []),
            ],
            recap.Workshops[0].Lessons,
            new RecapLessonComparer());
        Assert.Equal(RecapOutcome.MissedQuestions, (await RecapOfAsync(early)).Workshops[1].Outcome);
    }

    [Fact]
    public async Task Joining_during_a_session_counts_only_the_questions_that_started_after()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(2, 0, 0));
        var early = await meeting.JoinAsync();
        var session = await meeting.StartSessionAsync("unit-tests");
        _time.Advance(TimeSpan.FromSeconds(1));
        var late = await meeting.JoinAsync();

        await meeting.FinishSessionAsync(session, new()
        {
            [early] = _ => 1,
            [late] = id => id == session.Questions[0].StableId ? null : 1,
        });

        Assert.Equal(RecapOutcome.PerfectScore, Assert.Single((await RecapOfAsync(late)).Workshops).Outcome);
    }

    [Fact]
    public async Task Workshops_appear_in_course_order_and_those_without_a_session_are_left_out()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(1, 0, 0));
        var participant = await meeting.JoinAsync();

        await meeting.RunSessionAsync("test-doubles", new() { [participant] = _ => 1 });
        var withOne = await RecapOfAsync(participant);
        await meeting.RunSessionAsync("unit-tests", new() { [participant] = _ => 1 });
        var withBoth = await RecapOfAsync(participant);

        Assert.Equal([(2, "Test doubles")], withOne.Workshops.Select(w => (w.Number, w.Title)));
        Assert.Equal([(1, "Unit tests"), (2, "Test doubles")], withBoth.Workshops.Select(w => (w.Number, w.Title)));
    }

    [Fact]
    public async Task A_running_session_is_left_out_until_it_ends()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(1, 0, 0));
        var participant = await meeting.JoinAsync();
        var session = await meeting.StartSessionAsync("unit-tests");
        await Engine.SubmitAnswerAsync(participant, session.FirstQuestion.QuestionId, 2);
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        await Engine.RevealQuestionAsync(session.Id);

        var whileRunning = await RecapOfAsync(participant);
        await Engine.AdvanceSessionAsync(session.Id);
        var afterEnd = await RecapOfAsync(participant);

        Assert.Empty(whileRunning.Workshops);
        Assert.Equal(RecapOutcome.MissedQuestions, Assert.Single(afterEnd.Workshops).Outcome);
    }

    [Fact]
    public async Task The_recap_says_whose_it_is_and_has_its_own_token_per_participant()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(1, 0, 0));
        var first = await meeting.JoinAsync();
        var second = await meeting.JoinAsync();

        var firstToken = await Engine.GetRecapTokenAsync(first);
        var secondToken = await Engine.GetRecapTokenAsync(second);
        var recap = await Engine.GetRecapAsync(firstToken!);

        Assert.Matches("^[0-9a-f]{64}$", firstToken);
        Assert.NotEqual(firstToken, secondToken);
        Assert.NotEqual(first, firstToken);
        Assert.Equal((await Engine.GetParticipantAsync(first))!.Nickname, recap!.Nickname);
        Assert.Equal(
            (meeting.MasterclassId, "Dev team 2026", MasterclassStatus.Open, "Testing Fundamentals"),
            (recap.MasterclassId, recap.MasterclassName, recap.MasterclassStatus, recap.CourseTitle));
        Assert.Empty(recap.Workshops);
    }

    [Fact]
    public async Task Unknown_tokens_have_no_recap()
    {
        Assert.Null(await Engine.GetRecapTokenAsync("no-such-participant"));
        Assert.Null(await Engine.GetRecapAsync(new string('0', 64)));
    }

    // Records holding lists compare the lists by reference; compare a Lesson's Questions by content.
    private sealed class RecapLessonComparer : IEqualityComparer<RecapLesson>
    {
        public bool Equals(RecapLesson? x, RecapLesson? y) =>
            x is not null && y is not null
            && (x.Number, x.Title, x.Url) == (y.Number, y.Title, y.Url)
            && x.Questions.SequenceEqual(y.Questions);

        public int GetHashCode(RecapLesson obj) => obj.Number;
    }

    private async Task<Recap> RecapOfAsync(string participantToken)
    {
        var recapToken = await Engine.GetRecapTokenAsync(participantToken);
        Assert.NotNull(recapToken);
        var recap = await Engine.GetRecapAsync(recapToken);
        Assert.NotNull(recap);
        return recap;
    }

    private async Task<Meeting> OpenMeetingAsync(JsonObject importFile)
    {
        var import = await Engine.ImportCourseAsync(importFile.ToJsonString());
        Assert.Empty(import.Problems);
        var created = await Engine.CreateMasterclassAsync(import.Outcome!.CourseId, "Dev team 2026");
        var opened = await Engine.OpenMasterclassAsync(created.Masterclass!.Id);
        var workshops = (await Engine.ListCoursesAsync()).Single(c => c.Id == import.Outcome.CourseId).Workshops;
        return new Meeting(this, opened!.Id, opened.JoinCode!, workshops.ToDictionary(w => w.Slug, w => w.Id));
    }

    /// <summary>An Open Masterclass whose Sessions a test runs from start to end.</summary>
    private sealed record Meeting(RecapTests Test, int MasterclassId, string JoinCode, IReadOnlyDictionary<string, int> WorkshopIds)
    {
        public async Task<string> JoinAsync() => (await Test.Engine.JoinMasterclassAsync(JoinCode, null))!.Token;

        /// <summary>
        /// Starts the Workshop's Session with all its Easy Questions and runs it to its end. For each Question,
        /// each listed Participant answers the option the function picks from the Question's stable id (null:
        /// no Answer, so Timed out); option 1 is always the correct one.
        /// </summary>
        public async Task<int> RunSessionAsync(string workshopSlug, Dictionary<string, Func<string, int?>> answers, int easy = 0)
        {
            var started = await StartSessionAsync(workshopSlug, easy);
            await FinishSessionAsync(started, answers);
            return started.Id;
        }

        public async Task<StartedSession> StartSessionAsync(string workshopSlug, int easy = 0)
        {
            if (easy == 0)
            {
                easy = (await Test.Engine.ListCoursesAsync())
                    .SelectMany(c => c.Workshops).Single(w => w.Id == WorkshopIds[workshopSlug]).Questions.Easy;
            }
            var started = await Test.Engine.StartSessionAsync(MasterclassId, WorkshopIds[workshopSlug], new DifficultyCounts(easy, 0, 0));
            Assert.Empty(started.Problems);
            return started.Session!;
        }

        public async Task FinishSessionAsync(StartedSession session, Dictionary<string, Func<string, int?>> answers)
        {
            foreach (var question in session.Questions)
            {
                foreach (var (token, choose) in answers)
                {
                    if (choose(question.StableId) is { } option)
                    {
                        Assert.Equal(AnswerResult.Accepted, (await Test.Engine.SubmitAnswerAsync(token, question.QuestionId, option)).Result);
                    }
                }
                Test._time.Advance(TimeSpan.FromMilliseconds(3_500));
                Assert.NotNull(await Test.Engine.RevealQuestionAsync(session.Id));
                await Test.Engine.AdvanceSessionAsync(session.Id);
            }
            Assert.Equal(SessionStep.Ended, (await Test.Engine.GetSessionProgressAsync(session.Id))!.Step);
        }
    }
}
