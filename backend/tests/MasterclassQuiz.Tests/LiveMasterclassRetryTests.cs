using MasterclassQuiz.Api.Database;
using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Data.SqlClient;
using static MasterclassQuiz.Tests.LiveMasterclassFixture;

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
        _live = new LiveMasterclassFixture(sqlServer, database: _database);
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

        var revealed = await AdvanceUntilBroadcastAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
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

    // Moves the clock on in small steps, each once the backend timer waits on it, until it broadcasts
    // <paramref name="message"/>: the timer's retry delay is its own business, so the test doesn't assume it.
    private async Task<T> AdvanceUntilBroadcastAsync<T>(string message)
    {
        var step = TimeSpan.FromMilliseconds(100);
        for (var advanced = TimeSpan.Zero; advanced < TimeSpan.FromMinutes(1); advanced += step)
        {
            await _live.Time.WaitForTimerAsync(TimeSpan.FromSeconds(15));
            if (_live.Broadcasts.TryNext(out var heard))
            {
                Assert.Equal(message, heard.Message);
                return Assert.IsType<T>(heard.Payload);
            }
            _live.Time.Advance(step);
        }
        throw new TimeoutException($"No {message} within a minute on the clock.");
    }

    private async Task SetDatabaseAsync(string state)
    {
        await using var master = new SqlConnection(_sqlServer.ConnectionFor("master").ConnectionString);
        await master.OpenAsync();
        await using var command = new SqlCommand($"ALTER DATABASE [{DatabaseName}] SET {state};", master);
        await command.ExecuteNonQueryAsync();
    }
}
