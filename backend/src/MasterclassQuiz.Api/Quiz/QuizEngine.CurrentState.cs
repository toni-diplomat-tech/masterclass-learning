using System.Data;
using Dapper;

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
    /// outcome only once revealed), and whether the Masterclass is Closed. Null if there is no such Masterclass.
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
        return await QueryListAsync<int>("dbo.ListRunningSessions", parameters, cancellationToken);
    }

    private Task<CurrentStateRow?> ReadCurrentStateAsync(int masterclassId, string? participantToken, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        parameters.Add("Token", participantToken, DbType.AnsiString, size: 200);
        return QuerySingleOrDefaultAsync<CurrentStateRow>("dbo.GetCurrentState", parameters, cancellationToken);
    }

    // Null when the Question read is not the one the row is about (the Session moved on meanwhile).
    private async Task<CurrentState?> ToCurrentStateAsync(CurrentStateRow row, CancellationToken cancellationToken)
    {
        var step = row.Step;
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

        var leaderboard = step is LiveStep.Revealed or LiveStep.Ended
            ? await GetSessionLeaderboardAsync(row.SessionId!.Value, cancellationToken)
            : null;
        var participant = row.ParticipantId is { } participantId
            ? new ParticipantState(
                participantId,
                row.Nickname!,
                step is LiveStep.Question or LiveStep.Revealed
                    ? new OwnAnswer(row.QuestionId!.Value, row.CanAnswer, row.OptionPosition, (AnswerOutcome?)row.Outcome)
                    : null,
                leaderboard?.Entries.SingleOrDefault(entry => entry.ParticipantId == participantId))
            : null;
        return new CurrentState(row.MasterclassId, (MasterclassStatus)row.MasterclassStatus, step, question, count, revealed, ended, participant, leaderboard);
    }

    private sealed class CurrentStateRow
    {
        public int MasterclassId { get; init; }
        public int MasterclassStatus { get; init; }
        public int? SessionId { get; init; }
        public LiveStep Step { get; init; }
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
