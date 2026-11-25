using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.HostResults;

/// <summary>The Host's results endpoint: a thin caller of the Quiz engine.</summary>
public static class ResultEndpoints
{
    /// <summary>
    /// Maps <c>GET masterclasses/{id}/results</c> (the Masterclass's results, Open or Closed; 404 if there is
    /// no such Masterclass) on the Host's <c>/api/host</c> group, which requires the Host session. The
    /// results hold every Participant's Recap token, so they must stay Host-only.
    /// </summary>
    public static RouteGroupBuilder MapResultEndpoints(this RouteGroupBuilder host)
    {
        host.MapGet("/masterclasses/{id:int}/results", async (int id, QuizEngine engine, CancellationToken cancellationToken) =>
            await engine.GetMasterclassResultsAsync(id, cancellationToken) is { } results
                ? Results.Ok(results)
                : Results.NotFound());

        return host;
    }
}
