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
