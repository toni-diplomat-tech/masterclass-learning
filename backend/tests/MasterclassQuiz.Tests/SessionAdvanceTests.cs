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
        var meeting = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 2);
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        await Engine.RevealQuestionAsync(meeting.SessionId);

        _time.Advance(TimeSpan.FromMilliseconds(1_999));
        var duringPause = await Engine.AdvanceSessionAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        var afterPause = await Engine.AdvanceSessionAsync(meeting.SessionId);

        Assert.Equal(new SessionAdvance(null, null), duringPause);
        var next = afterPause.Started!;
        Assert.Null(afterPause.Ended);
        Assert.Equal((2, 2, meeting.Questions[1].QuestionId), (next.Number, next.QuestionCount, next.QuestionId));
        Assert.Equal((_time.GetUtcNow().UtcDateTime, _time.GetUtcNow().UtcDateTime.AddSeconds(3)), (next.StartedAt, next.Deadline));
    }

    [Fact]
    public async Task A_running_question_is_not_skipped()
    {
        var meeting = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 2);
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(new SessionAdvance(null, null), await Engine.AdvanceSessionAsync(meeting.SessionId));
        Assert.Equal(1, (await Engine.GetStartedQuestionAsync(meeting.SessionId))!.Number);
    }

    [Fact]
    public async Task The_session_ends_on_its_own_after_the_pause_following_its_last_reveal()
    {
        var meeting = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 1);
        var revealed = await Engine.RevealQuestionAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));

        var advanced = await Engine.AdvanceSessionAsync(meeting.SessionId);
        var again = await Engine.AdvanceSessionAsync(meeting.SessionId);

        Assert.True(revealed!.LastQuestion);
        Assert.Equal(new SessionAdvance(null, new SessionEnded(meeting.MasterclassId, meeting.SessionId, _time.GetUtcNow().UtcDateTime)), advanced);
        Assert.Equal(new SessionAdvance(null, null), again);
    }

    [Fact]
    public async Task Once_a_session_ended_the_masterclass_can_run_another_one()
    {
        var meeting = await RunningSessionFixture.StartAsync(Engine, participants: 0, easy: 1);
        var masterclass = (await Engine.GetMasterclassAsync(meeting.MasterclassId))!;
        var otherWorkshopId = (await Engine.ListCoursesAsync())
            .Single(c => c.Id == masterclass.CourseId).Workshops
            .Single(w => w.Slug != SessionFiles.WorkshopSlug).Id;
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        await Engine.RevealQuestionAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));
        await Engine.AdvanceSessionAsync(meeting.SessionId);

        var next = await Engine.StartSessionAsync(meeting.MasterclassId, otherWorkshopId, new DifficultyCounts(1, 0, 0));

        Assert.Empty(next.Problems);
    }

    [Fact]
    public async Task The_progress_says_what_the_session_waits_for_and_until_when()
    {
        var meeting = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        var running = await Engine.GetSessionProgressAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromSeconds(4));
        await Engine.RevealQuestionAsync(meeting.SessionId);
        var revealed = await Engine.GetSessionProgressAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));
        await Engine.AdvanceSessionAsync(meeting.SessionId);
        var ended = await Engine.GetSessionProgressAsync(meeting.SessionId);

        Assert.Equal(new SessionProgress(meeting.SessionId, meeting.MasterclassId, SessionStep.QuestionRunning, Now.UtcDateTime.AddMilliseconds(3_500)), running);
        Assert.Equal(new SessionProgress(meeting.SessionId, meeting.MasterclassId, SessionStep.QuestionRevealed, Now.UtcDateTime.AddSeconds(6)), revealed);
        Assert.Equal(new SessionProgress(meeting.SessionId, meeting.MasterclassId, SessionStep.Ended, null), ended);
        Assert.Null(await Engine.GetSessionProgressAsync(-1));
    }
}
