using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MasterclassQuiz.Api.HostAuth;

/// <summary>
/// The single Host's cookie session. Login checks the password against the hash in the
/// <c>HOST_PASSWORD_HASH</c> setting (an environment variable); every Host endpoint, and later the
/// Host's SignalR actions, requires the session. Without a hash configured, every login is refused.
/// </summary>
public static class HostSession
{
    /// <summary>The cookie the frontend relays to the browser and forwards back on its server-side calls.</summary>
    public const string CookieName = "mq_host";

    public const string PasswordHashSetting = "HOST_PASSWORD_HASH";

    /// <summary>Fixed, not sliding: the frontend relays the cookie only at login.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public static IServiceCollection AddHostSession(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = Lifetime;
                options.SlidingExpiration = false;
                // An API answers 401/403; redirecting to a login page is the frontend's job.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Maps login, logout and the session check under <c>/api/host</c>, and returns that group:
    /// endpoints added to it require the Host session.
    /// </summary>
    public static RouteGroupBuilder MapHostApi(this WebApplication app)
    {
        var passwordHash = app.Configuration[PasswordHashSetting];
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            app.Logger.LogWarning("{Setting} is not set: every Host login will be refused.", PasswordHashSetting);
        }

        var host = app.MapGroup("/api/host").RequireAuthorization();

        host.MapPost("/login", async (LoginRequest request, HttpContext context) =>
        {
            if (!HostPassword.Verify(request.Password ?? "", passwordHash))
            {
                return Results.Problem(title: "Wrong password.", statusCode: StatusCodes.Status401Unauthorized);
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, "Host")], CookieAuthenticationDefaults.AuthenticationScheme);
            // Persistent: survives a browser restart on the projector machine, until Lifetime runs out.
            await context.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
            return Results.NoContent();
        }).AllowAnonymous();

        host.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync();
            return Results.NoContent();
        }).AllowAnonymous();

        // Lets the frontend check a session before rendering a Host page.
        host.MapGet("/session", () => Results.Ok());

        return host;
    }

    private sealed record LoginRequest(string? Password);
}
