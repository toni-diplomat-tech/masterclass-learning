using Dapper;
using MasterclassQuiz.Api.Database;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Tests;

/// <summary>
/// Only one Masterclass can be Open at a time, but the tests share one database and leave their
/// Masterclasses Open, often with a Session still running. So before a test opens a Masterclass, it
/// closes every other Open one. That happens straight in the database: the Host's way, closing through
/// the Quiz engine, is refused while a Session runs.
/// </summary>
public static class OnlyOpenMasterclass
{
    /// <summary>Closes every Open Masterclass in <paramref name="database"/>, so the next one can open.</summary>
    public static async Task CloseOpenMasterclassesAsync(DatabaseConnection database)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.ExecuteAsync("UPDATE dbo.Masterclasses SET Status = 3, ClosedAt = SYSUTCDATETIME() WHERE Status = 2;");
    }

    /// <summary>
    /// Opens a Masterclass on the shared test database after closing every other Open one there, and
    /// checks that it opened.
    /// </summary>
    public static async Task<MasterclassOverview> OpenAsOnlyOpenMasterclassAsync(this QuizEngine engine, int masterclassId)
    {
        await CloseOpenMasterclassesAsync(SqlServerFixture.Shared);
        var opened = await engine.OpenMasterclassAsync(masterclassId);
        Assert.Empty(opened.Problems);
        return opened.Masterclass!;
    }
}
