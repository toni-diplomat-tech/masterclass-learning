using MasterclassQuiz.Api.Participants;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Recaps;

/// <summary>Recaps: thin callers of the Quiz engine, all anonymous.</summary>
public static class RecapEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/participants/me/recap</c> (<c>{recapToken}</c> of the cookie's Participant, 401
    /// without one) and <c>GET /api/recaps/{recapToken}</c> (the Recap, readable by anyone with the link and
    /// with no expiry; 404 problem for an unknown token).
    /// </summary>
    public static IEndpointRouteBuilder MapRecapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/participants/me/recap", async (HttpContext context, QuizEngine engine, CancellationToken cancellationToken) =>
            context.Request.Cookies[ParticipantEndpoints.CookieName] is { Length: > 0 } token
            && await engine.GetRecapTokenAsync(token, cancellationToken) is { } recapToken
                ? Results.Ok(new RecapLink(recapToken))
                : Results.Unauthorized());

        app.MapGet("/api/recaps/{recapToken}", async (string recapToken, QuizEngine engine, CancellationToken cancellationToken) =>
            await engine.GetRecapAsync(recapToken, cancellationToken) is { } recap
                ? Results.Ok(recap)
                : Results.Problem(title: "There is no such Recap.", statusCode: StatusCodes.Status404NotFound));

        return app;
    }

    private sealed record RecapLink(string RecapToken);
}
