using System.Data;
using Dapper;
using MasterclassQuiz.Api.Database;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Api.Quiz;

/// <summary>
/// The Quiz engine: the only code that calls stored procedures (ADR 0002).
/// Business rules live in the procedures; this class passes them the current UTC time as <c>@Now</c>.
/// </summary>
public sealed class QuizEngine(DatabaseConnection database, TimeProvider timeProvider)
{
    public async Task<DatabaseHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        var checkedAt = await connection.QuerySingleAsync<DateTime>(new CommandDefinition(
            "dbo.CheckHealth",
            WithNow(new DynamicParameters()),
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return new DatabaseHealth(DateTime.SpecifyKind(checkedAt, DateTimeKind.Utc));
    }

    private DynamicParameters WithNow(DynamicParameters parameters)
    {
        parameters.Add("Now", timeProvider.GetUtcNow().UtcDateTime, DbType.DateTime2);
        return parameters;
    }
}

/// <summary>Proof that the engine reached the database; the time is the engine's <c>@Now</c>.</summary>
public sealed record DatabaseHealth(DateTime CheckedAtUtc);
