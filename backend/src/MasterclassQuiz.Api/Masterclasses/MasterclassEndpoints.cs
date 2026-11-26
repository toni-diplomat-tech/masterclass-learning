using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Masterclasses;

/// <summary>The Host's Masterclass endpoints: thin callers of the Quiz engine.</summary>
public static class MasterclassEndpoints
{
    /// <summary>
    /// Maps <c>GET masterclasses</c>, <c>POST masterclasses</c> (<c>{courseId, name}</c>: a new Draft),
    /// <c>GET masterclasses/{id}</c>, <c>POST masterclasses/{id}/open</c>, <c>POST masterclasses/{id}/close</c>
    /// (broadcasts <c>MasterclassClosed</c>) and <c>GET masterclasses/{id}/leaderboard</c> (the Masterclass
    /// leaderboard) on the Host's <c>/api/host</c> group, which requires the Host session.
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

        host.MapPost("/masterclasses/{id:int}/close", async (int id, QuizEngine engine, MasterclassNotifier notifier, CancellationToken cancellationToken) =>
        {
            var result = await engine.CloseMasterclassAsync(id, cancellationToken);
            if (result.Masterclass is null)
            {
                return Results.NotFound();
            }
            if (!result.Succeeded)
            {
                return Results.Problem(
                    title: "The Masterclass was not closed.",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { ["problems"] = result.Problems });
            }
            await notifier.MasterclassClosedAsync(id, cancellationToken);
            return Results.Ok(result.Masterclass);
        });

        host.MapGet("/masterclasses/{id:int}/leaderboard", async (int id, QuizEngine engine, CancellationToken cancellationToken) =>
            await engine.GetMasterclassLeaderboardAsync(id, cancellationToken) is { } leaderboard
                ? Results.Ok(leaderboard)
                : Results.NotFound());

        return host;
    }

    private sealed record CreateRequest(int? CourseId, string? Name);
}
