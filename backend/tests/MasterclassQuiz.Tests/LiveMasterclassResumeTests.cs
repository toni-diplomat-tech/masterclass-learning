using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Tests;

/// <summary>
/// Resuming a Masterclass's running Sessions through the live Masterclass module (the projector's Resume
/// button): an overdue Question is revealed once and the Session carries on; resuming again, or resuming a
/// Session the backend timer already follows, broadcasts nothing twice; an ended Session stays ended.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class LiveMasterclassResumeTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly LiveMasterclassFixture _live = new(sqlServer, Settings);

    [Fact]
    public async Task Resuming_a_masterclass_with_an_overdue_question_reveals_it_once_and_the_session_carries_on()
    {
        var meeting = await OverdueSessionAsync(easy: 2);

        await _live.Live.ResumeMasterclassAsync(meeting.MasterclassId);

        var revealed = await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        Assert.Equal((meeting.Question.QuestionId, 0, 1), (revealed.QuestionId, revealed.Answered, revealed.TimedOut));
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        await _live.Time.WaitForTimerAsync();
        await _live.Broadcasts.AssertQuietAsync();

        await _live.Time.AdvanceWhenTimerWaitsAsync(RevealPause);

        var next = await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);
        Assert.Equal((meeting.SessionId, 2), (next.SessionId, next.Number));
    }

    [Fact]
    public async Task Resuming_twice_reveals_the_overdue_question_once()
    {
        var meeting = await OverdueSessionAsync(easy: 2);

        await Task.WhenAll(
            _live.Live.ResumeMasterclassAsync(meeting.MasterclassId),
            _live.Live.ResumeMasterclassAsync(meeting.MasterclassId));
        await _live.Live.ResumeMasterclassAsync(meeting.MasterclassId);

        await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        await _live.Time.WaitForTimerAsync();
        await _live.Broadcasts.AssertQuietAsync();

        await _live.Time.AdvanceWhenTimerWaitsAsync(RevealPause);

        await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);
        await _live.Time.WaitForTimerAsync();
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Resuming_a_session_the_timer_already_follows_broadcasts_nothing_more()
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, participants: 1, easy: 2);
        Assert.Empty(await _live.Live.StartSessionAsync(ready.MasterclassId, ready.WorkshopId, ready.Mix));
        await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);

        var resumed = await _live.Live.ResumeMasterclassAsync(ready.MasterclassId);

        Assert.Single(resumed);
        await _live.Broadcasts.AssertQuietAsync();
        await _live.Time.AdvanceWhenTimerWaitsAsync(AnswersClose);
        await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        await _live.Time.WaitForTimerAsync();
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Resuming_after_the_session_ended_does_nothing()
    {
        var meeting = await OverdueSessionAsync(easy: 1);
        Assert.NotNull(await _live.Engine.RevealQuestionAsync(meeting.SessionId));
        _live.Time.Advance(RevealPause);
        Assert.NotNull((await _live.Engine.AdvanceSessionAsync(meeting.SessionId)).Ended);

        var resumed = await _live.Live.ResumeMasterclassAsync(meeting.MasterclassId);

        Assert.Empty(resumed);
        await _live.Broadcasts.AssertQuietAsync();
        Assert.Equal(SessionStep.Ended, (await _live.Engine.GetSessionProgressAsync(meeting.SessionId))!.Step);
    }

    public void Dispose() => _live.Dispose();

    // Easy Questions have 3 seconds to answer, so Answers close 3.5 s after the start (with grace);
    // a revealed Question stays up 2 seconds.
    private static readonly QuizSettings Settings = new()
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3 },
        RevealPauseSeconds = 2,
    };

    private static readonly TimeSpan AnswersClose = TimeSpan.FromMilliseconds(3_500);
    private static readonly TimeSpan RevealPause = TimeSpan.FromSeconds(2);

    // A Session started through the engine (so no backend timer follows it, as after a hiccup or a restart)
    // whose first Question's Answers have closed: one Participant, who didn't answer.
    private async Task<RunningSessionFixture> OverdueSessionAsync(int easy)
    {
        var meeting = await RunningSessionFixture.StartAsync(_live.Engine, participants: 1, easy: easy);
        _live.Time.Advance(AnswersClose); // nobody waits on the clock yet
        return meeting;
    }
}
