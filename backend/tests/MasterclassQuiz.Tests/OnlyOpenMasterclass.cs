using Dapper;
using MasterclassQuiz.Api.Database;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Tests;

/// <summary>
/// Only one Masterclass can be Open at a time, but the tests share one database and leave their
/// Masterclasses Open, often with a Session still running. So before a test opens a Masterclass, it
/// closes every other Open one.
/// </summary>
public static class OnlyOpenMasterclass
{
    /// <summary>
    /// Closes every Open Masterclass in <paramref name="database"/> at <paramref name="time"/>'s now, so
    /// the next one can open.
    /// </summary>
    public static async Task CloseOpenMasterclassesAsync(DatabaseConnection database, TimeProvider time)
    {
        // Straight in the database, not through the Quiz engine: the engine refuses to close a Masterclass
        // while a Session runs, and ending another test's Session would mean playing it to its end.
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.ExecuteAsync(
            "UPDATE dbo.Masterclasses SET Status = @Closed, ClosedAt = @Now WHERE Status = @Open;",
            new { Closed = (byte)MasterclassStatus.Closed, Open = (byte)MasterclassStatus.Open, Now = time.GetUtcNow().UtcDateTime });
    }

    /// <summary>
    /// Opens a Masterclass after closing every other Open one in <paramref name="database"/> (the engine's
    /// database) at <paramref name="time"/>'s now, and checks that it opened.
    /// </summary>
    public static async Task<MasterclassOverview> OpenAsOnlyOpenMasterclassAsync(
        this QuizEngine engine, int masterclassId, DatabaseConnection database, TimeProvider time)
    {
        await CloseOpenMasterclassesAsync(database, time);
        var opened = await engine.OpenMasterclassAsync(masterclassId);
        Assert.Empty(opened.Problems);
        return opened.Masterclass!;
    }
}
