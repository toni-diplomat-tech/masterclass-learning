using System.Text.Json.Nodes;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>
/// The Host's results of a Masterclass: per Session and per Question the Correct, Incorrect and Timed out
/// counts and the percentage correct, and every Participant by Nickname with their points and Recap token.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class ResultsTests(SqlServerFixture sqlServer)
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
    public async Task Each_question_counts_correct_incorrect_and_timed_out_answers_and_the_percentage_correct()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(2, 0, 0));
        var (a, b, c) = (await meeting.JoinAsync(), await meeting.JoinAsync(), await meeting.JoinAsync());
        var session = await meeting.StartSessionAsync("unit-tests");

        await meeting.FinishSessionAsync(session, new()
        {
            [a] = _ => 1,
            [b] = id => id == session.Questions[0].StableId ? 2 : 1,
            [c] = id => id == session.Questions[0].StableId ? null : 3,
        });

        var results = await Engine.GetMasterclassResultsAsync(meeting.MasterclassId);
        var sessionResults = Assert.Single(results!.Sessions);
        Assert.Equal((session.Id, 1, "Unit tests", true), (sessionResults.SessionId, sessionResults.WorkshopNumber, sessionResults.WorkshopTitle, sessionResults.Ended));
        Assert.Equal(
            [
                new QuestionResult(1, Difficulty.Easy, $"Question {session.Questions[0].StableId}?", 1, 1, 1, 33),
                new QuestionResult(2, Difficulty.Easy, $"Question {session.Questions[1].StableId}?", 2, 1, 0, 67),
            ],
            sessionResults.Questions);
    }

    [Fact]
    public async Task Sessions_are_in_course_order_and_a_running_question_appears_once_revealed()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(2, 0, 0));
        var participant = await meeting.JoinAsync();
        var doubles = await meeting.StartSessionAsync("test-doubles");
        await meeting.FinishSessionAsync(doubles, new() { [participant] = _ => 1 });
        var unitTests = await meeting.StartSessionAsync("unit-tests");
        await Engine.SubmitAnswerAsync(participant, unitTests.FirstQuestion.QuestionId, 1);

        var beforeReveal = await Engine.GetMasterclassResultsAsync(meeting.MasterclassId);
        await meeting.AnswerAndRevealAsync(unitTests.Id, unitTests.Questions[0], []);
        var afterReveal = await Engine.GetMasterclassResultsAsync(meeting.MasterclassId);

        Assert.Equal(
            [("Unit tests", false, 0), ("Test doubles", true, doubles.Questions.Count)],
            beforeReveal!.Sessions.Select(s => (s.WorkshopTitle, s.Ended, s.Questions.Count)));
        var revealed = Assert.Single(afterReveal!.Sessions[0].Questions);
        Assert.Equal((1, 1, 0, 0, 100), (revealed.Number, revealed.Correct, revealed.Incorrect, revealed.TimedOut, revealed.PercentCorrect));
    }

    [Fact]
    public async Task Every_participant_is_listed_by_nickname_with_their_points_over_all_sessions_and_their_recap_token()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(2, 0, 0));
        var (allRight, oneRight) = (await meeting.JoinAsync(), await meeting.JoinAsync());
        var unitTests = await meeting.StartSessionAsync("unit-tests");
        await meeting.FinishSessionAsync(unitTests, new()
        {
            [allRight] = _ => 1,
            [oneRight] = id => id == unitTests.Questions[0].StableId ? 1 : 2,
        });
        var late = await meeting.JoinAsync();
        var doubles = await meeting.StartSessionAsync("test-doubles");
        await meeting.FinishSessionAsync(doubles, new() { [allRight] = _ => 1 });

        var participants = (await Engine.GetMasterclassResultsAsync(meeting.MasterclassId))!.Participants;

        // Answered at once, every Correct Easy Answer scores the full 500 points.
        var expected = new[] { (allRight, 500 * (2 + doubles.Questions.Count)), (oneRight, 500), (late, 0) };
        var rows = new List<ParticipantResult>();
        foreach (var (token, points) in expected)
        {
            var participant = (await Engine.GetParticipantAsync(token))!;
            rows.Add(new ParticipantResult(participant.Id, participant.Nickname, points, (await Engine.GetRecapTokenAsync(token))!));
        }
        Assert.Equal(rows.OrderBy(r => r.Nickname, StringComparer.OrdinalIgnoreCase), participants);
    }

    [Fact]
    public async Task A_masterclass_without_sessions_has_no_results_yet_and_an_unknown_one_has_none()
    {
        var meeting = await OpenMeetingAsync(SessionFiles.WithWorkshopQuestions(1, 0, 0));

        var results = await Engine.GetMasterclassResultsAsync(meeting.MasterclassId);

        Assert.Equal(
            (meeting.MasterclassId, "Dev team 2026", MasterclassStatus.Open, "Testing Fundamentals"),
            (results!.MasterclassId, results.MasterclassName, results.MasterclassStatus, results.CourseTitle));
        Assert.Empty(results.Sessions);
        Assert.Null(await Engine.GetMasterclassResultsAsync(-1));
    }

    private async Task<Meeting> OpenMeetingAsync(JsonObject importFile)
    {
        var import = await Engine.ImportCourseAsync(importFile.ToJsonString());
        Assert.Empty(import.Problems);
        var created = await Engine.CreateMasterclassAsync(import.Summary!.CourseId, "Dev team 2026");
        var opened = await Engine.OpenMasterclassAsync(created.Masterclass!.Id);
        var workshops = (await Engine.ListCoursesAsync()).Single(c => c.Id == import.Summary.CourseId).Workshops;
        return new Meeting(this, opened!.Id, opened.JoinCode!, workshops.ToDictionary(w => w.Slug, w => (w.Id, w.Questions.Easy)));
    }

    /// <summary>An Open Masterclass whose Sessions a test runs.</summary>
    private sealed record Meeting(ResultsTests Test, int MasterclassId, string JoinCode, IReadOnlyDictionary<string, (int Id, int Easy)> Workshops)
    {
        public async Task<string> JoinAsync() => (await Test.Engine.JoinMasterclassAsync(JoinCode, null))!.Token;

        /// <summary>Starts the Workshop's Session with all its Easy Questions.</summary>
        public async Task<StartedSession> StartSessionAsync(string workshopSlug)
        {
            var (id, easy) = Workshops[workshopSlug];
            var started = await Test.Engine.StartSessionAsync(MasterclassId, id, new DifficultyCounts(easy, 0, 0));
            Assert.Empty(started.Problems);
            return started.Session!;
        }

        /// <summary>
        /// Runs the Session's remaining Questions to its end. For each Question, each listed Participant answers
        /// the option the function picks from the Question's stable id (null: no Answer, so Timed out); option 1
        /// is always the correct one.
        /// </summary>
        public async Task FinishSessionAsync(StartedSession session, Dictionary<string, Func<string, int?>> answers)
        {
            foreach (var question in session.Questions)
            {
                await AnswerAndRevealAsync(session.Id, question, answers);
                await Test.Engine.AdvanceSessionAsync(session.Id);
            }
            Assert.Equal(SessionStep.Ended, (await Test.Engine.GetSessionProgressAsync(session.Id))!.Step);
        }

        public async Task AnswerAndRevealAsync(int sessionId, SessionQuestion question, Dictionary<string, Func<string, int?>> answers)
        {
            foreach (var (token, choose) in answers)
            {
                if (choose(question.StableId) is { } option)
                {
                    Assert.Equal(AnswerResult.Accepted, (await Test.Engine.SubmitAnswerAsync(token, question.QuestionId, option)).Result);
                }
            }
            Test._time.Advance(TimeSpan.FromMilliseconds(3_500));
            Assert.NotNull(await Test.Engine.RevealQuestionAsync(sessionId));
        }
    }
}
