using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Masterclasses;

/// <summary>The Host's Masterclass endpoints: thin callers of the Quiz engine.</summary>
public static class MasterclassEndpoints
{
    /// <summary>
    /// Maps <c>GET masterclasses</c>, <c>POST masterclasses</c> (<c>{courseId, name}</c>: a new Draft),
    /// <c>GET masterclasses/{id}</c> and <c>POST masterclasses/{id}/open</c> on the Host's <c>/api/host</c>
    /// group, which requires the Host session.
    /// </summary>
    public static RouteGroupBuilder MapMasterclassEndpoints(this RouteGroupBuilder host)
    {
        host.MapGet("/masterclasses", (QuizEngine engine, CancellationToken cancellationToken) =>
            engine.ListMasterclassesAsync(cancellationToken));

        host.MapPost("/masterclasses", async (CreateRequest request, QuizEngine engine, CancellationToken cancellationToken) =>
        {
            var result = await engine.CreateMasterclassAsync(request.CourseId ?? 0, request.Name ?? "", cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/host/masterclasses/{result.Masterclass!.Id}", result.Masterclass)
                : Results.Problem(
                    title: "The Masterclass was not created.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["problems"] = result.Problems });
        });

        host.MapGet("/masterclasses/{id:int}", async (int id, QuizEngine engine, CancellationToken cancellationToken) =>
            await engine.GetMasterclassAsync(id, cancellationToken) is { } masterclass
                ? Results.Ok(masterclass)
                : Results.NotFound());

        host.MapPost("/masterclasses/{id:int}/open", async (int id, QuizEngine engine, CancellationToken cancellationToken) =>
            await engine.OpenMasterclassAsync(id, cancellationToken) switch
            {
                null => Results.NotFound(),
                { Status: MasterclassStatus.Open } masterclass => Results.Ok(masterclass),
                _ => Results.Problem(
                    title: "A Closed Masterclass can't be opened again.",
                    statusCode: StatusCodes.Status409Conflict),
            });

        return host;
    }

    private sealed record CreateRequest(int? CourseId, string? Name);
}
