namespace MasterclassQuiz.Api.Quiz;

/// <summary>
/// A Session's leaderboard (the <c>LeaderboardUpdated</c> message): every Participant with an Answer to one
/// of its revealed Questions, highest points first. <see cref="Ended"/>: the Session is over, so this is final.
/// </summary>
public sealed record SessionLeaderboard(
    int MasterclassId,
    int SessionId,
    string WorkshopTitle,
    bool Ended,
    IReadOnlyList<LeaderboardEntry> Entries);

/// <summary>
/// One Participant on a leaderboard. Participants with the same <see cref="Points"/> share a
/// <see cref="Rank"/> (1, 1, 3). <see cref="QuestionPoints"/> is what they scored on the latest revealed Question.
/// </summary>
public sealed record LeaderboardEntry(int Rank, int ParticipantId, string Nickname, int Points, int QuestionPoints);

/// <summary>
/// The Masterclass leaderboard, shown at the end of the meeting: every Participant with an Answer to a revealed
/// Question of any of its Sessions, by points summed over all Sessions, highest first.
/// </summary>
public sealed record MasterclassLeaderboard(int MasterclassId, IReadOnlyList<MasterclassLeaderboardEntry> Entries);

/// <summary>One Participant on the Masterclass leaderboard. Equal <see cref="Points"/> share a <see cref="Rank"/> (1, 1, 3).</summary>
public sealed record MasterclassLeaderboardEntry(int Rank, int ParticipantId, string Nickname, int Points);

/// <summary>
/// A Participant's rank on their Masterclass leaderboard: their <see cref="Rank"/> and <see cref="Points"/>,
/// out of the <see cref="Of"/> Participants on it.
/// </summary>
public sealed record MasterclassRank(int Rank, int Points, int Of);
