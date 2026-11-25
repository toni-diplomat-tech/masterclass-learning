using Dapper;

namespace MasterclassQuiz.Api.Quiz;

// Results: the Host's view of how a Masterclass went, per Session, per Question and per Participant.
public sealed partial class QuizEngine
{
    /// <summary>
    /// The Masterclass's results for the Host, whatever its status; null if there is no such Masterclass.
    /// Only revealed Questions count, so a running Question appears once it is revealed.
    /// </summary>
    public async Task<MasterclassResults?> GetMasterclassResultsAsync(int masterclassId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        return ProcedureJson.Read<MasterclassResults>(await QueryJsonAsync("dbo.GetMasterclassResults", parameters, cancellationToken));
    }
}
