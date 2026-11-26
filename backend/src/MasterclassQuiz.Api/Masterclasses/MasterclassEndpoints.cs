using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Masterclasses;

/// <summary>
/// The Host's Masterclass endpoints: thin callers of the Quiz engine, or of the live Masterclass module
/// (<see cref="LiveMasterclass"/>) for a change that is broadcast.
/// </summary>
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

        // Straight to the Quiz engine, not through LiveMasterclass: a Draft isn't live yet, so nobody is
        // listening for it to open (the Host's own page refreshes), and a refused open changes nothing.
        host.MapPost("/masterclasses/{id:int}/open", async (int id, QuizEngine engine, CancellationToken cancellationToken) =>
        {
            var result = await engine.OpenMasterclassAsync(id, cancellationToken);
            return Answer(result.Masterclass, result.Succeeded, result.Problems, "The Masterclass was not opened.");
        });

        host.MapPost("/masterclasses/{id:int}/close", async (int id, LiveMasterclass live, CancellationToken cancellationToken) =>
        {
            var result = await live.CloseMasterclassAsync(id, cancellationToken);
            return Answer(result.Masterclass, result.Succeeded, result.Problems, "The Masterclass was not closed.");
        });

        host.MapGet("/masterclasses/{id:int}/leaderboard", async (int id, QuizEngine engine, CancellationToken cancellationToken) =>
            await engine.GetMasterclassLeaderboardAsync(id, cancellationToken) is { } leaderboard
                ? Results.Ok(leaderboard)
                : Results.NotFound());

        return host;
    }

    // A change to one Masterclass: 404 when there is none, 409 with its problems when it was refused,
    // otherwise 200 with the Masterclass as it now is.
    private static IResult Answer(MasterclassOverview? masterclass, bool succeeded, IReadOnlyList<string> problems, string refusedTitle)
    {
        if (masterclass is null)
        {
            return Results.NotFound();
        }
        return succeeded
            ? Results.Ok(masterclass)
            : Results.Problem(
                title: refusedTitle,
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["problems"] = problems });
    }

    private sealed record CreateRequest(int? CourseId, string? Name);
}
