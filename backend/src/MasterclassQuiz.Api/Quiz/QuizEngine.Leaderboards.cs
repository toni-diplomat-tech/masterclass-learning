using Dapper;

namespace MasterclassQuiz.Api.Quiz;

// Leaderboards: the points Answers scored, summed and ranked.
public sealed partial class QuizEngine
{
    /// <summary>
    /// The Session's leaderboard over its revealed Questions (a running Question's Answers don't count
    /// until its reveal); null if there is no such Session.
    /// </summary>
    public async Task<SessionLeaderboard?> GetSessionLeaderboardAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SessionId", sessionId);
        return ProcedureJson.Read<SessionLeaderboard>(await QueryJsonAsync("dbo.GetSessionLeaderboard", parameters, cancellationToken));
    }

    /// <summary>
    /// The Masterclass leaderboard: points summed over all its Sessions' revealed Questions; null if there is
    /// no such Masterclass.
    /// </summary>
    public async Task<MasterclassLeaderboard?> GetMasterclassLeaderboardAsync(int masterclassId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        return ProcedureJson.Read<MasterclassLeaderboard>(await QueryJsonAsync("dbo.GetMasterclassLeaderboard", parameters, cancellationToken));
    }
}
