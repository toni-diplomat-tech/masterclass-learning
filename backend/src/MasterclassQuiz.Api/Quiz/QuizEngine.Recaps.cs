using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Api.Quiz;

// Recaps: each Participant's look back at a Masterclass, at an unguessable link of its own.
public sealed partial class QuizEngine
{
    /// <summary>
    /// The token of the Participant's Recap link (<c>/recap/&lt;token&gt;</c>); null if the Participant
    /// token belongs to nobody. It is a separate secret from the Participant token, so sharing the Recap
    /// link never lets anyone answer as the Participant.
    /// </summary>
    public async Task<string?> GetRecapTokenAsync(string participantToken, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Token", participantToken, DbType.AnsiString, size: 200);
        await using var connection = new SqlConnection(database.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "dbo.GetRecapToken",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// The Recap behind this Recap token; null if it belongs to nobody. Readable without logging in, at any
    /// time, including after the Masterclass is closed. A Session's part appears only once it has ended.
    /// </summary>
    public async Task<Recap?> GetRecapAsync(string recapToken, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("RecapToken", recapToken, DbType.AnsiString, size: 200);
        return ProcedureJson.Read<Recap>(await QueryJsonAsync("dbo.GetRecap", parameters, cancellationToken));
    }
}
