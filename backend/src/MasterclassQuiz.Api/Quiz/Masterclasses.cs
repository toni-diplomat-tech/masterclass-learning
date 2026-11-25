using System.Text.Json.Serialization;

namespace MasterclassQuiz.Api.Quiz;

/// <summary>Draft: set up in the back office. Open: the Join code works. Closed: no more joining or Sessions.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MasterclassStatus>))]
public enum MasterclassStatus
{
    Draft = 1,
    Open = 2,
    Closed = 3,
}

/// <summary>A Masterclass as the Host sees it. Times are UTC; <see cref="JoinCode"/> is null until it opens.</summary>
public sealed record MasterclassOverview(
    int Id,
    string Name,
    int CourseId,
    string CourseTitle,
    MasterclassStatus Status,
    string? JoinCode,
    int Participants,
    DateTime CreatedAt,
    DateTime? OpenedAt,
    DateTime? ClosedAt);

/// <summary>The created Masterclass, or every reason it wasn't created (then <see cref="Masterclass"/> is null).</summary>
public sealed record MasterclassCreateResult(MasterclassOverview? Masterclass, IReadOnlyList<string> Problems)
{
    public bool Succeeded => Problems.Count == 0;
}

/// <summary>
/// The Masterclass after a close (null if there is none), and every reason it wasn't closed (empty: it is Closed).
/// </summary>
public sealed record MasterclassCloseResult(MasterclassOverview? Masterclass, IReadOnlyList<string> Problems)
{
    public bool Succeeded => Problems.Count == 0 && Masterclass is { Status: MasterclassStatus.Closed };
}

/// <summary>A person in a Masterclass, known by their assigned Nickname. <see cref="JoinedAt"/> is UTC.</summary>
public sealed record Participant(
    int Id,
    int MasterclassId,
    string MasterclassName,
    MasterclassStatus MasterclassStatus,
    string Nickname,
    DateTime JoinedAt);

/// <summary>
/// A joined Participant and the secret token that identifies them from now on (kept in their browser's cookie).
/// </summary>
public sealed record ParticipantJoin(Participant Participant, string Token);
