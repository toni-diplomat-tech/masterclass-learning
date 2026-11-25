using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Api.Quiz;

// Current state: what a (re)connecting phone or projector should show, and which Sessions still run.
public sealed partial class QuizEngine
{
    // The Session can move on between reading where it stands and reading its Question; then read again.
    private const int CurrentStateAttempts = 3;

    /// <summary>
    /// What the Masterclass's screens should show now: waiting, the running Question (never its correct
    /// option) with its answer count, the latest revealed Question, or the end of the latest Session. With
    /// the token of a Participant of this Masterclass, also their own Answer to the Question shown (its
    /// outcome only once revealed). Null if there is no such Masterclass.
    /// </summary>
    public async Task<CurrentState?> GetCurrentStateAsync(int masterclassId, string? participantToken, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var row = await ReadCurrentStateAsync(masterclassId, participantToken, cancellationToken);
            if (row is null)
            {
                return null;
            }
            var state = await ToCurrentStateAsync(row, cancellationToken);
            if (state is not null || attempt == CurrentStateAttempts)
            {
                return state ?? throw new InvalidOperationException($"Masterclass {masterclassId} kept moving on while its state was read.");
            }
        }
    }

    /// <summary>
    /// The Sessions that haven't ended, which the backend timer must follow (e.g. again after a restart);
    /// only <paramref name="masterclassId"/>'s when given.
    /// </summary>
    public async Task<IReadOnlyList<int>> ListRunningSessionsAsync(int? masterclassId = null, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        await using var connection = new SqlConnection(database.ConnectionString);
        return (await connection.QueryAsync<int>(new CommandDefinition(
            "dbo.ListRunningSessions",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken))).ToList();
    }

    private async Task<CurrentStateRow?> ReadCurrentStateAsync(int masterclassId, string? participantToken, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        parameters.Add("Token", participantToken, DbType.AnsiString, size: 200);
        await using var connection = new SqlConnection(database.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<CurrentStateRow>(new CommandDefinition(
            "dbo.GetCurrentState",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    // Null when the Question read is not the one the row is about (the Session moved on meanwhile).
    private async Task<CurrentState?> ToCurrentStateAsync(CurrentStateRow row, CancellationToken cancellationToken)
    {
        var step = Enum.Parse<LiveStep>(row.Step);
        QuestionStarted? question = null;
        AnswerCount? count = null;
        QuestionRevealed? revealed = null;
        SessionEnded? ended = null;
        switch (step)
        {
            case LiveStep.Question:
                question = await GetStartedQuestionAsync(row.SessionId!.Value, cancellationToken);
                if (question is null || question.QuestionId != row.QuestionId)
                {
                    return null;
                }
                count = new AnswerCount(row.MasterclassId, row.SessionId.Value, question.QuestionId, row.Answered, row.Participants);
                break;
            case LiveStep.Revealed:
                revealed = await GetRevealedQuestionAsync(row.SessionId!.Value, cancellationToken);
                if (revealed is null || revealed.QuestionId != row.QuestionId)
                {
                    return null;
                }
                break;
            case LiveStep.Ended:
                ended = new SessionEnded(row.MasterclassId, row.SessionId!.Value, Utc(row.EndedAt!.Value));
                break;
        }

        var participant = row.ParticipantId is { } participantId
            ? new ParticipantState(
                participantId,
                row.Nickname!,
                step is LiveStep.Question or LiveStep.Revealed
                    ? new OwnAnswer(row.QuestionId!.Value, row.CanAnswer, row.OptionPosition, (AnswerOutcome?)row.Outcome)
                    : null)
            : null;
        return new CurrentState(row.MasterclassId, step, question, count, revealed, ended, participant);
    }

    private sealed class CurrentStateRow
    {
        public int MasterclassId { get; init; }
        public int? SessionId { get; init; }
        public string Step { get; init; } = "";
        public DateTime? EndedAt { get; init; }
        public int? QuestionId { get; init; }
        public int Answered { get; init; }
        public int Participants { get; init; }
        public int? ParticipantId { get; init; }
        public string? Nickname { get; init; }
        public bool CanAnswer { get; init; }
        public int? OptionPosition { get; init; }
        public byte? Outcome { get; init; }
    }
}
