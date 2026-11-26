using System.Data;
using System.Globalization;
using Dapper;

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

        var (problems, id) = await QueryUnlessProblemsAsync(
            "dbo.CreateMasterclass", WithNow(parameters), results => results.ReadSingleAsync<int>(), cancellationToken);
        return problems.Count > 0
            ? new MasterclassCreateResult(null, problems)
            : new MasterclassCreateResult(await GetMasterclassAsync(id, cancellationToken), []);
    }

    /// <summary>
    /// Opens a Draft Masterclass with a 6-digit Join code. Only one Masterclass can be Open at a time, so a
    /// Draft can't open while another Masterclass is Open, and a Closed one can't open again. Opening the
    /// Open one again keeps its code. <see cref="MasterclassOpenResult.Masterclass"/> is the Masterclass as
    /// it now is (null if there is none).
    /// </summary>
    public async Task<MasterclassOpenResult> OpenMasterclassAsync(int masterclassId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        var problems = await QueryListAsync<string>("dbo.OpenMasterclass", WithNow(WithSeed(parameters)), cancellationToken);
        return new MasterclassOpenResult(await GetMasterclassAsync(masterclassId, cancellationToken), problems);
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
        var problems = await QueryListAsync<string>("dbo.CloseMasterclass", WithNow(parameters), cancellationToken);
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
        var rows = await QueryListAsync<MasterclassRow>("dbo.ListMasterclasses", parameters, cancellationToken);
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
        public int? NextWorkshopId { get; init; }
        public string? WorkshopIdsWithSession { get; init; } // comma-separated

        public MasterclassOverview ToOverview() => new(
            Id, Name, CourseId, CourseTitle, (MasterclassStatus)Status, JoinCode, Participants,
            Utc(CreatedAt), OpenedAt is { } opened ? Utc(opened) : null, ClosedAt is { } closed ? Utc(closed) : null,
            NextWorkshopId,
            WorkshopIdsWithSession?.Split(',').Select(id => int.Parse(id, CultureInfo.InvariantCulture)).ToList() ?? []);
    }
}
