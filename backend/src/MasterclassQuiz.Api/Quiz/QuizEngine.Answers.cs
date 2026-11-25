using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MasterclassQuiz.Api.Quiz;

// Answers: Participants answering the running Question.
public sealed partial class QuizEngine
{
    /// <summary>
    /// The Participant whose token this is answers a Question of their Masterclass's running Session with
    /// the option at <paramref name="optionPosition"/>. It counts if the server's time now is no later than
    /// the Question's deadline plus 500 ms grace and the Question isn't revealed yet; a Participant answers
    /// a Question at most once, can't change it, and can't answer a Question that started before they joined.
    /// </summary>
    public async Task<AnswerSubmission> SubmitAnswerAsync(string participantToken, int questionId, int optionPosition, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Token", participantToken, DbType.AnsiString, size: 200);
        parameters.Add("QuestionId", questionId);
        parameters.Add("OptionPosition", optionPosition);
        await using var connection = new SqlConnection(database.ConnectionString);
        var row = await connection.QuerySingleAsync<AnswerRow>(new CommandDefinition(
            "dbo.SubmitAnswer",
            WithNow(parameters),
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return new AnswerSubmission(
            Enum.Parse<AnswerResult>(row.Result),
            row.SessionId is { } sessionId
                ? new AnswerCount(row.MasterclassId!.Value, sessionId, row.QuestionId!.Value, row.Answered!.Value, row.Participants!.Value)
                : null);
    }

    private sealed class AnswerRow
    {
        public string Result { get; init; } = "";
        public int? MasterclassId { get; init; }
        public int? SessionId { get; init; }
        public int? QuestionId { get; init; }
        public int? Answered { get; init; }
        public int? Participants { get; init; }
    }
}
