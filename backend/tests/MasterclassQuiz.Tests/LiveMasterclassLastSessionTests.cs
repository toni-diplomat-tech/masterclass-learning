using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;
using static MasterclassQuiz.Tests.LiveMasterclassFixture;

namespace MasterclassQuiz.Tests;

/// <summary>
/// A Masterclass ends on its own when its last Workshop's Session ends: the example Course has two
/// Workshops, "unit-tests" then "test-doubles", each with one Easy Question here.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class LiveMasterclassLastSessionTests(SqlServerFixture sqlServer) : IDisposable
{
    private const string LastWorkshopSlug = "test-doubles";

    private readonly LiveMasterclassFixture _live = new(sqlServer);

    [Fact]
    public async Task Ending_the_last_workshops_session_closes_the_masterclass_and_broadcasts_it_after_the_end()
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, _live.Database, _live.Time, participants: 1, easy: 1);
        await RunSessionAsync(ready.MasterclassId, ready.WorkshopId);
        await _live.Broadcasts.NextAsync<SessionEnded>(QuizHub.SessionEnded);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);

        var lastSessionId = await RunSessionAsync(ready.MasterclassId, await _live.Engine.WorkshopIdAsync(ready.MasterclassId, LastWorkshopSlug));

        var ended = await _live.Broadcasts.NextAsync<SessionEnded>(QuizHub.SessionEnded);
        Assert.Equal(lastSessionId, ended.SessionId);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        var closed = await _live.Broadcasts.NextAsync<MasterclassClosedMessage>(QuizHub.MasterclassClosed);
        Assert.Equal(ready.MasterclassId, closed.MasterclassId);
        var masterclass = await _live.Engine.GetMasterclassAsync(ready.MasterclassId);
        Assert.Equal((MasterclassStatus.Closed, ended.EndedAt), (masterclass!.Status, masterclass.ClosedAt));
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Ending_an_earlier_workshops_session_leaves_the_masterclass_open()
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, _live.Database, _live.Time, participants: 1, easy: 1);

        await RunSessionAsync(ready.MasterclassId, ready.WorkshopId);

        await _live.Broadcasts.NextAsync<SessionEnded>(QuizHub.SessionEnded);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        await _live.Broadcasts.AssertQuietAsync();
        Assert.Equal(MasterclassStatus.Open, (await _live.Engine.GetMasterclassAsync(ready.MasterclassId))!.Status);
    }

    [Fact]
    public async Task The_host_can_still_close_the_masterclass_between_sessions()
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, _live.Database, _live.Time, participants: 1, easy: 1);
        await RunSessionAsync(ready.MasterclassId, ready.WorkshopId);
        await _live.Broadcasts.NextAsync<SessionEnded>(QuizHub.SessionEnded);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);

        var closed = await _live.Live.CloseMasterclassAsync(ready.MasterclassId);

        Assert.Equal(MasterclassStatus.Closed, closed.Masterclass!.Status);
        await _live.Broadcasts.NextAsync<MasterclassClosedMessage>(QuizHub.MasterclassClosed);
        await _live.Broadcasts.AssertQuietAsync();
    }

    public void Dispose() => _live.Dispose();

    // Starts the Workshop's one-Easy-Question Session through the module and lets the backend timer run it
    // to its end; takes every broadcast up to (not including) its SessionEnded. Returns the Session's id.
    private async Task<int> RunSessionAsync(int masterclassId, int workshopId)
    {
        Assert.Empty(await _live.Live.StartSessionAsync(masterclassId, workshopId, new DifficultyCounts(1, 0, 0)));
        var started = await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);
        await _live.Time.AdvanceWhenTimerWaitsAsync(AnswersClose);
        await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        await _live.Time.AdvanceWhenTimerWaitsAsync(RevealPause);
        return started.SessionId;
    }
}
