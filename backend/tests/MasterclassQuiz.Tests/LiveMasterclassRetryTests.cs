using MasterclassQuiz.Api.Database;
using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Tests;

/// <summary>
/// The database goes away mid-Session: the backend timer keeps retrying, and once the database is back the
/// Session carries on. Runs on its own database, taken offline and back, so the shared one is untouched.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class LiveMasterclassRetryTests : IDisposable
{
    private const string DatabaseName = "LiveRetry";

    private readonly SqlServerFixture _sqlServer;
    private readonly DatabaseConnection _database;
    private readonly LiveMasterclassFixture _live;

    public LiveMasterclassRetryTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
        // No reconnect retries: an offline database fails the Quiz engine's call at once.
        _database = new DatabaseConnection(new SqlConnectionStringBuilder(sqlServer.ConnectionFor(DatabaseName).ConnectionString) { ConnectRetryCount = 0 }.ConnectionString);
        DatabaseMigrator.Migrate(_database, DatabaseMigrator.DefaultScriptsDirectory);
        _live = new LiveMasterclassFixture(sqlServer, Settings, _database);
    }

    [Fact]
    public async Task After_a_quiz_engine_error_the_timer_retries_and_the_session_carries_on_once_the_database_is_back()
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, participants: 1, easy: 2);
        Assert.Empty(await _live.Live.StartSessionAsync(ready.MasterclassId, ready.WorkshopId, ready.Mix));
        var first = await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);
        await _live.Time.WaitForTimerAsync();

        await SetDatabaseAsync("OFFLINE WITH ROLLBACK IMMEDIATE");
        await _live.Time.AdvanceWhenTimerWaitsAsync(AnswersClose);
        await _live.Time.WaitForTimerAsync(TimeSpan.FromSeconds(15)); // the reveal failed; waiting to retry
        await _live.Broadcasts.AssertQuietAsync();

        await SetDatabaseAsync("ONLINE");
        SqlConnection.ClearPool(new SqlConnection(_database.ConnectionString));
        await _live.Time.AdvanceWhenTimerWaitsAsync(RetryDelay);

        var revealed = await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed, TimeSpan.FromSeconds(10));
        Assert.Equal((first.QuestionId, 1), (revealed.QuestionId, revealed.TimedOut));
        await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        await _live.Time.AdvanceWhenTimerWaitsAsync(RevealPause);
        var next = await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);
        Assert.Equal((first.SessionId, 2), (next.SessionId, next.Number));
    }

    public void Dispose()
    {
        _live.Dispose();
        SetDatabaseAsync("ONLINE").GetAwaiter().GetResult();
    }

    // Easy Questions have 3 seconds to answer (Answers close 3.5 s after the start, with grace); a revealed
    // Question stays up 2 seconds; the backend timer retries a failed step after 1 second.
    private static readonly QuizSettings Settings = new()
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3 },
        RevealPauseSeconds = 2,
    };

    private static readonly TimeSpan AnswersClose = TimeSpan.FromMilliseconds(3_500);
    private static readonly TimeSpan RevealPause = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private async Task SetDatabaseAsync(string state)
    {
        await using var master = new SqlConnection(_sqlServer.ConnectionFor("master").ConnectionString);
        await master.OpenAsync();
        await using var command = new SqlCommand($"ALTER DATABASE [{DatabaseName}] SET {state};", master);
        await command.ExecuteNonQueryAsync();
    }
}
