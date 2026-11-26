using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;
using static MasterclassQuiz.Tests.LiveMasterclassFixture;

namespace MasterclassQuiz.Tests;

/// <summary>
/// A Session run through the live Masterclass module: starting it, then the backend timer revealing each
/// Question at its deadline, starting the next after the reveal pause, and ending the Session.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class LiveMasterclassSessionTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly LiveMasterclassFixture _live = new(sqlServer);

    [Fact]
    public async Task Starting_a_session_broadcasts_its_first_question()
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, participants: 1, easy: 2);

        var problems = await _live.Live.StartSessionAsync(ready.MasterclassId, ready.WorkshopId, ready.Mix);

        Assert.Empty(problems);
        var first = await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);
        Assert.Equal((ready.MasterclassId, 1, 2), (first.MasterclassId, first.Number, first.QuestionCount));
        Assert.Equal(_live.Time.GetUtcNow().UtcDateTime, first.StartedAt);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task A_refused_start_broadcasts_nothing()
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, participants: 1, easy: 1);

        var problems = await _live.Live.StartSessionAsync(ready.MasterclassId, ready.WorkshopId, new DifficultyCounts(2, 0, 0));

        Assert.Equal(["The Difficulty mix asks for 2 Questions but the Workshop has only 1."], problems);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Advancing_past_the_deadline_reveals_the_question_once_then_the_leaderboard()
    {
        var first = await StartAsync(participants: 2, easy: 2);

        await _live.Time.AdvanceWhenTimerWaitsAsync(AnswersClose);

        var revealed = await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        Assert.Equal((first.QuestionId, 0, 2, false), (revealed.QuestionId, revealed.Answered, revealed.TimedOut, revealed.LastQuestion));
        Assert.Equal(_live.Time.GetUtcNow().UtcDateTime, revealed.RevealedAt);
        var leaderboard = await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        Assert.Equal((first.SessionId, false), (leaderboard.SessionId, leaderboard.Ended));
        await _live.Time.WaitForTimerAsync();
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Advancing_past_the_reveal_pause_starts_the_next_question()
    {
        var first = await StartAsync(participants: 1, easy: 2);
        await _live.Time.AdvanceWhenTimerWaitsAsync(AnswersClose);
        await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);

        await _live.Time.AdvanceWhenTimerWaitsAsync(RevealPause);

        var next = await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);
        Assert.Equal((first.SessionId, 2, 2), (next.SessionId, next.Number, next.QuestionCount));
        Assert.NotEqual(first.QuestionId, next.QuestionId);
        Assert.Equal(_live.Time.GetUtcNow().UtcDateTime, next.StartedAt);
        await _live.Time.WaitForTimerAsync();
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task After_the_last_question_the_session_ends_with_a_final_leaderboard()
    {
        var first = await StartAsync(participants: 1, easy: 1);
        await _live.Time.AdvanceWhenTimerWaitsAsync(AnswersClose);
        var revealed = await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        Assert.True(revealed.LastQuestion);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);

        await _live.Time.AdvanceWhenTimerWaitsAsync(RevealPause);

        var ended = await _live.Broadcasts.NextAsync<SessionEnded>(QuizHub.SessionEnded);
        Assert.Equal((first.MasterclassId, first.SessionId, _live.Time.GetUtcNow().UtcDateTime), (ended.MasterclassId, ended.SessionId, ended.EndedAt));
        var final = await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        Assert.Equal((first.SessionId, true), (final.SessionId, final.Ended));
        Assert.Equal(SessionStep.Ended, (await _live.Engine.GetSessionProgressAsync(first.SessionId))!.Step);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Once_the_session_has_ended_the_timer_stops_following_it()
    {
        await StartAsync(participants: 1, easy: 1);
        await _live.Time.AdvanceWhenTimerWaitsAsync(AnswersClose);
        await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        await _live.Time.AdvanceWhenTimerWaitsAsync(RevealPause);
        await _live.Broadcasts.NextAsync<SessionEnded>(QuizHub.SessionEnded);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);

        await Assert.ThrowsAsync<TimeoutException>(() => _live.Time.WaitForTimerAsync(TimeSpan.FromMilliseconds(500)));
        _live.Time.Advance(TimeSpan.FromMinutes(10));
        await _live.Broadcasts.AssertQuietAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => _live.Time.WaitForTimerAsync(TimeSpan.FromMilliseconds(200)));
    }

    public void Dispose() => _live.Dispose();

    // Starts a Session through the module and takes its first QuestionStarted broadcast.
    private async Task<QuestionStarted> StartAsync(int participants, int easy)
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, participants, easy);
        Assert.Empty(await _live.Live.StartSessionAsync(ready.MasterclassId, ready.WorkshopId, ready.Mix));
        return await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);
    }
}
