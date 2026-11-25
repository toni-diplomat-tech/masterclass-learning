using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Api.Quiz;

// Masterclasses: creating, listing and opening them.
public sealed partial class QuizEngine
{
    /// <summary>Creates a Draft Masterclass named <paramref name="name"/> for a Course.</summary>
    public async Task<MasterclassCreateResult> CreateMasterclassAsync(int courseId, string name, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("CourseId", courseId);
        parameters.Add("Name", name, DbType.String, size: -1);

        int id;
        await using (var connection = new SqlConnection(database.ConnectionString))
        {
            await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
                "dbo.CreateMasterclass",
                WithNow(parameters),
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
            var problems = (await results.ReadAsync<string>()).ToList();
            if (problems.Count > 0)
            {
                return new MasterclassCreateResult(null, problems);
            }
            id = await results.ReadSingleAsync<int>();
        }
        return new MasterclassCreateResult(await GetMasterclassAsync(id, cancellationToken), []);
    }

    /// <summary>
    /// Opens a Draft Masterclass with a 6-digit Join code unique among Open Masterclasses. An Open one keeps
    /// its code and a Closed one stays Closed. Returns the Masterclass as it now is; null if there is none.
    /// </summary>
    public async Task<MasterclassOverview?> OpenMasterclassAsync(int masterclassId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        await using (var connection = new SqlConnection(database.ConnectionString))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "dbo.OpenMasterclass",
                WithNow(WithSeed(parameters)),
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        return await GetMasterclassAsync(masterclassId, cancellationToken);
    }

    /// <summary>
    /// Closes an Open Masterclass when the meeting ends: no more joining or Sessions, and its Join code is
    /// free for other Open Masterclasses; Recaps stay readable. A Draft can't be closed, nor an Open one while
    /// a Session runs. Closing a Closed one again changes nothing. <see cref="MasterclassCloseResult.Masterclass"/>
    /// is the Masterclass as it now is (null if there is none).
    /// </summary>
    public async Task<MasterclassCloseResult> CloseMasterclassAsync(int masterclassId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        List<string> problems;
        await using (var connection = new SqlConnection(database.ConnectionString))
        {
            problems = (await connection.QueryAsync<string>(new CommandDefinition(
                "dbo.CloseMasterclass",
                WithNow(parameters),
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).ToList();
        }
        return new MasterclassCloseResult(await GetMasterclassAsync(masterclassId, cancellationToken), problems);
    }

    /// <summary>Every Masterclass, newest first.</summary>
    public async Task<IReadOnlyList<MasterclassOverview>> ListMasterclassesAsync(CancellationToken cancellationToken = default) =>
        await QueryMasterclassesAsync(null, cancellationToken);

    /// <summary>One Masterclass; null if there is no such Masterclass.</summary>
    public async Task<MasterclassOverview?> GetMasterclassAsync(int masterclassId, CancellationToken cancellationToken = default) =>
        (await QueryMasterclassesAsync(masterclassId, cancellationToken)).SingleOrDefault();

    private async Task<IReadOnlyList<MasterclassOverview>> QueryMasterclassesAsync(int? masterclassId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        await using var connection = new SqlConnection(database.ConnectionString);
        var rows = await connection.QueryAsync<MasterclassRow>(new CommandDefinition(
            "dbo.ListMasterclasses",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return rows.Select(row => row.ToOverview()).ToList();
    }

    private sealed class MasterclassRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public int CourseId { get; init; }
        public string CourseTitle { get; init; } = "";
        public int Status { get; init; }
        public string? JoinCode { get; init; }
        public int Participants { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? OpenedAt { get; init; }
        public DateTime? ClosedAt { get; init; }

        public MasterclassOverview ToOverview() => new(
            Id, Name, CourseId, CourseTitle, (MasterclassStatus)Status, JoinCode, Participants,
            Utc(CreatedAt), OpenedAt is { } opened ? Utc(opened) : null, ClosedAt is { } closed ? Utc(closed) : null);
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
