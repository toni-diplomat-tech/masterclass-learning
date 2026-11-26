using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>After each reveal and its pause, the Session moves on by itself, and ends after its last Question.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class SessionAdvanceTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    // Easy Questions have 3 seconds to answer (3.5 s with grace); a revealed Question stays up 2 seconds.
    private readonly FakeTimeProvider _time = new(Now);
    private QuizEngine Engine => new(sqlServer.Connection, _time, settings: new QuizSettings
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3 },
        RevealPauseSeconds = 2,
    });

    [Fact]
    public async Task The_next_question_starts_after_the_configured_reveal_pause()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 2);
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        await Engine.RevealQuestionAsync(session.SessionId);

        _time.Advance(TimeSpan.FromMilliseconds(1_999));
        var duringPause = await Engine.AdvanceSessionAsync(session.SessionId);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        var afterPause = await Engine.AdvanceSessionAsync(session.SessionId);

        Assert.Equal(new SessionAdvance(null, null), duringPause);
        var next = afterPause.Started!;
        Assert.Null(afterPause.Ended);
        Assert.Equal((2, 2, session.Questions[1].QuestionId), (next.Number, next.QuestionCount, next.QuestionId));
        Assert.Equal((_time.GetUtcNow().UtcDateTime, _time.GetUtcNow().UtcDateTime.AddSeconds(3)), (next.StartedAt, next.Deadline));
    }

    [Fact]
    public async Task A_running_question_is_not_skipped()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 2);
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(new SessionAdvance(null, null), await Engine.AdvanceSessionAsync(session.SessionId));
        Assert.Equal(1, (await Engine.GetStartedQuestionAsync(session.SessionId))!.Number);
    }

    [Fact]
    public async Task The_session_ends_on_its_own_after_the_pause_following_its_last_reveal()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        var revealed = await Engine.RevealQuestionAsync(session.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));

        var advanced = await Engine.AdvanceSessionAsync(session.SessionId);
        var again = await Engine.AdvanceSessionAsync(session.SessionId);

        Assert.True(revealed!.LastQuestion);
        Assert.Equal(new SessionAdvance(null, new SessionEnded(session.MasterclassId, session.SessionId, _time.GetUtcNow().UtcDateTime)), advanced);
        Assert.Equal(new SessionAdvance(null, null), again);
    }

    [Fact]
    public async Task Once_a_session_ended_the_masterclass_can_run_another_one()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 0, easy: 1);
        var masterclass = (await Engine.GetMasterclassAsync(session.MasterclassId))!;
        var otherWorkshopId = (await Engine.ListCoursesAsync())
            .Single(c => c.Id == masterclass.CourseId).Workshops
            .Single(w => w.Slug != SessionFiles.WorkshopSlug).Id;
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        await Engine.RevealQuestionAsync(session.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));
        await Engine.AdvanceSessionAsync(session.SessionId);

        var next = await Engine.StartSessionAsync(session.MasterclassId, otherWorkshopId, new DifficultyCounts(1, 0, 0));

        Assert.Empty(next.Problems);
    }

    [Fact]
    public async Task The_progress_says_what_the_session_waits_for_and_until_when()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1);
        var running = await Engine.GetSessionProgressAsync(session.SessionId);
        _time.Advance(TimeSpan.FromSeconds(4));
        await Engine.RevealQuestionAsync(session.SessionId);
        var revealed = await Engine.GetSessionProgressAsync(session.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));
        await Engine.AdvanceSessionAsync(session.SessionId);
        var ended = await Engine.GetSessionProgressAsync(session.SessionId);

        Assert.Equal(new SessionProgress(session.SessionId, session.MasterclassId, SessionStep.QuestionRunning, Now.UtcDateTime.AddMilliseconds(3_500)), running);
        Assert.Equal(new SessionProgress(session.SessionId, session.MasterclassId, SessionStep.QuestionRevealed, Now.UtcDateTime.AddSeconds(6)), revealed);
        Assert.Equal(new SessionProgress(session.SessionId, session.MasterclassId, SessionStep.Ended, null), ended);
        Assert.Null(await Engine.GetSessionProgressAsync(-1));
    }
}
