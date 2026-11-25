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
