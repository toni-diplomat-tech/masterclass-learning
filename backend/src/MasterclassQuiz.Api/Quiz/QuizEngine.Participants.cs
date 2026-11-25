using System.Data;
using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Api.Quiz;

// Participants: joining a Masterclass with its Join code and being recognized again by token.
public sealed partial class QuizEngine
{
    /// <summary>
    /// Joins the Open Masterclass whose Join code this is. A browser whose <paramref name="currentToken"/>
    /// already belongs to a Participant of that Masterclass gets that Participant back; otherwise a new
    /// Participant gets a Nickname unique within the Masterclass and a new random token.
    /// Null when no Open Masterclass has this Join code.
    /// </summary>
    public async Task<ParticipantJoin?> JoinMasterclassAsync(string joinCode, string? currentToken, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("JoinCode", joinCode, DbType.String, size: 50);
        parameters.Add("CurrentToken", currentToken, DbType.AnsiString, size: 200);
        parameters.Add("NewToken", NewParticipantToken(), DbType.AnsiString, size: 200);
        await using var connection = new SqlConnection(database.ConnectionString);
        var row = await connection.QuerySingleOrDefaultAsync<ParticipantRow>(new CommandDefinition(
            "dbo.JoinMasterclass",
            WithNow(WithSeed(parameters)),
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return row is null ? null : new ParticipantJoin(row.ToParticipant(), row.Token!);
    }

    /// <summary>The Participant this token belongs to; null if it belongs to nobody.</summary>
    public async Task<Participant?> GetParticipantAsync(string token, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Token", token, DbType.AnsiString, size: 200);
        await using var connection = new SqlConnection(database.ConnectionString);
        var row = await connection.QuerySingleOrDefaultAsync<ParticipantRow>(new CommandDefinition(
            "dbo.GetParticipant",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return row?.ToParticipant();
    }

    // 256 random bits, URL-safe: unguessable, and safe in a cookie.
    private static string NewParticipantToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class ParticipantRow
    {
        public string? Token { get; init; }
        public int Id { get; init; }
        public int MasterclassId { get; init; }
        public string MasterclassName { get; init; } = "";
        public int MasterclassStatus { get; init; }
        public string Nickname { get; init; } = "";
        public DateTime JoinedAt { get; init; }

        public Participant ToParticipant() =>
            new(Id, MasterclassId, MasterclassName, (MasterclassStatus)MasterclassStatus, Nickname, Utc(JoinedAt));
    }
}
