using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Participants;

/// <summary>
/// Joining a Masterclass from a phone: thin callers of the live Masterclass module (join) and the Quiz
/// engine (reads). A Participant is remembered by an HTTP-only cookie holding their random token; there
/// are no accounts.
/// </summary>
public static class ParticipantEndpoints
{
    /// <summary>The cookie the frontend relays to the browser and forwards back on its server-side calls.</summary>
    public const string CookieName = "mq_participant";

    /// <summary>Long enough for the meeting and its aftermath; the Recap has its own link.</summary>
    public static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// Maps <c>POST /api/participants/join</c> (<c>{joinCode}</c>: sets the cookie and answers with the
    /// Participant, 404 problem for a code of no Open Masterclass), <c>GET /api/participants/me</c>
    /// (the cookie's Participant, 401 without one) and <c>GET /api/participants/me/place</c> (their
    /// <c>{rank, points, of}</c> on the Masterclass leaderboard; 404 while they are not on it, 401 without one).
    /// </summary>
    public static IEndpointRouteBuilder MapParticipantEndpoints(this IEndpointRouteBuilder app)
    {
        var participants = app.MapGroup("/api/participants");

        participants.MapPost("/join", async (JoinRequest request, HttpContext context, LiveMasterclass live, CancellationToken cancellationToken) =>
        {
            var joined = await live.JoinMasterclassAsync(request.JoinCode ?? "", context.Request.Cookies[CookieName], cancellationToken);
            if (joined is null)
            {
                return Results.Problem(title: "No open Masterclass has this Join code.", statusCode: StatusCodes.Status404NotFound);
            }

            context.Response.Cookies.Append(CookieName, joined.Token, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Path = "/",
                MaxAge = CookieLifetime,
            });
            return Results.Ok(joined.Participant);
        });

        participants.MapGet("/me", async (HttpContext context, QuizEngine engine, CancellationToken cancellationToken) =>
            context.Request.Cookies[CookieName] is { Length: > 0 } token
            && await engine.GetParticipantAsync(token, cancellationToken) is { } participant
                ? Results.Ok(participant)
                : Results.Unauthorized());

        participants.MapGet("/me/place", async (HttpContext context, QuizEngine engine, CancellationToken cancellationToken) =>
        {
            if (context.Request.Cookies[CookieName] is not { Length: > 0 } token
                || await engine.GetParticipantAsync(token, cancellationToken) is null)
            {
                return Results.Unauthorized();
            }
            return await engine.GetMasterclassPlaceAsync(token, cancellationToken) is { } place
                ? Results.Ok(place)
                : Results.NotFound();
        });

        return app;
    }

    private sealed record JoinRequest(string? JoinCode);
}
