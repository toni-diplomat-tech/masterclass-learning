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
        var created = await Engine.CreateMasterclassAsync(import.Summary!.CourseId, "Dev team 2026");
        var opened = await Engine.OpenMasterclassAsync(created.Masterclass!.Id);
        var workshops = (await Engine.ListCoursesAsync()).Single(c => c.Id == import.Summary.CourseId).Workshops;
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
