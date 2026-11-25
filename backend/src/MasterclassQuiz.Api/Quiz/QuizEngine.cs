using System.Data;
using Dapper;
using MasterclassQuiz.Api.Database;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Api.Quiz;

/// <summary>
/// The Quiz engine: the only code that calls stored procedures (ADR 0002).
/// Business rules live in the procedures; this class passes them the current UTC time as <c>@Now</c>
/// and, where a rule draws at random (Join codes, Nicknames), a <c>@Seed</c> from <paramref name="random"/>
/// (default <see cref="Random.Shared"/>), so a test can make the draws repeat. Time limits come from
/// <paramref name="settings"/> (configuration; default: the spec's values).
/// </summary>
public sealed partial class QuizEngine(DatabaseConnection database, TimeProvider timeProvider, Random? random = null, QuizSettings? settings = null)
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

    /// <summary>
    /// Creates or updates a Course from an import file's contents (format: docs/import-file/README.md). The
    /// import is an upsert keyed on slugs and Question ids: Questions missing from the file are retired,
    /// never deleted. A file with any problem is rejected as a whole.
    /// </summary>
    public async Task<CourseImportResult> ImportCourseAsync(string fileContents, CancellationToken cancellationToken = default)
    {
        var (json, problem) = ImportFile.Normalize(fileContents);
        if (json is null)
        {
            return CourseImportResult.Rejected(problem!);
        }

        await using var connection = new SqlConnection(database.ConnectionString);
        var parameters = new DynamicParameters();
        parameters.Add("File", json, DbType.String, size: -1);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            "dbo.ImportCourse",
            WithNow(parameters),
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var problems = (await results.ReadAsync<string>()).ToList();
        if (problems.Count > 0)
        {
            return CourseImportResult.Rejected(problems);
        }
        return new CourseImportResult(await results.ReadSingleAsync<CourseImportSummary>(), []);
    }

    /// <summary>Every Course with its current Workshops in order and their Question counts per Difficulty.</summary>
    public async Task<IReadOnlyList<CourseOverview>> ListCoursesAsync(CancellationToken cancellationToken = default)
    {
        var json = await QueryJsonAsync("dbo.ListCourses", new DynamicParameters(), cancellationToken);
        return ProcedureJson.Read<List<CourseOverview>>(json) ?? [];
    }

    /// <summary>A Course's current Workshops and Lessons in order, with their Questions; null if there is no such Course.</summary>
    public async Task<CourseDetail?> GetCourseAsync(string courseSlug, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("CourseSlug", courseSlug);
        return ProcedureJson.Read<CourseDetail>(await QueryJsonAsync("dbo.GetCourse", parameters, cancellationToken));
    }

    // For procedures that return one nvarchar(max) built with FOR JSON (null when there is nothing).
    private async Task<string?> QueryJsonAsync(string procedure, DynamicParameters parameters, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        return await connection.QuerySingleAsync<string?>(new CommandDefinition(
            procedure,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    private DynamicParameters WithSeed(DynamicParameters parameters)
    {
        parameters.Add("Seed", (random ?? Random.Shared).Next());
        return parameters;
    }

    private DynamicParameters WithNow(DynamicParameters parameters)
    {
        parameters.Add("Now", timeProvider.GetUtcNow().UtcDateTime, DbType.DateTime2);
        return parameters;
    }
}

/// <summary>Proof that the engine reached the database; the time is the engine's <c>@Now</c>.</summary>
public sealed record DatabaseHealth(DateTime CheckedAtUtc);
