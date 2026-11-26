using System.Text.Json.Serialization;
using MasterclassQuiz.Api.Courses;
using MasterclassQuiz.Api.Database;
using MasterclassQuiz.Api.HostAuth;
using MasterclassQuiz.Api.HostResults;
using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Masterclasses;
using MasterclassQuiz.Api.Participants;
using MasterclassQuiz.Api.Quiz;
using MasterclassQuiz.Api.Recaps;

// `hash-host-password`: read a password from stdin and print the value for HOST_PASSWORD_HASH.
if (args is ["hash-host-password"])
{
    Console.Error.Write("Host password: ");
    var password = Console.ReadLine();
    if (string.IsNullOrEmpty(password))
    {
        Console.Error.WriteLine("No password given.");
        return 1;
    }
    Console.WriteLine(HostPassword.Hash(password));
    return 0;
}

var builder = WebApplication.CreateBuilder(args);

var database = new DatabaseConnection(
    builder.Configuration.GetConnectionString("Quiz")
    ?? throw new InvalidOperationException("Connection string 'Quiz' is not set."));

builder.Services.AddSingleton(database);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(QuizSettingsFrom(builder.Configuration));
builder.Services.AddSingleton<QuizEngine>();
builder.Services.AddHostSession();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ParticipantNotifier>();
builder.Services.AddSingleton<SessionNotifier>();
builder.Services.AddSingleton<IMasterclassBroadcasts, SignalRBroadcasts>();
builder.Services.AddSingleton<LiveMasterclass>();
builder.Services.AddSingleton<SessionRunner>();
if (builder.Configuration.GetValue(SessionRecovery.EnabledKey, defaultValue: true))
{
    builder.Services.AddHostedService<SessionRecovery>();
}

var app = builder.Build();

DatabaseMigrator.Migrate(database, DatabaseMigrator.DefaultScriptsDirectory);

// Below the frontend health page's 5 s fetch timeout.
var databaseHealthTimeout = TimeSpan.FromSeconds(3);

app.MapGet("/health", async (QuizEngine engine, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    try
    {
        // Answer inside the frontend's fetch timeout even when the database is down. SqlClient can
        // block for its whole connect timeout (15 s) before yielding, ignoring cancellation, so run
        // the check on the thread pool and stop waiting for it after a few seconds.
        var health = await Task.Run(() => engine.CheckHealthAsync(cancellationToken), cancellationToken)
            .WaitAsync(databaseHealthTimeout, cancellationToken);
        return Results.Ok(new HealthResponse(HealthStatus.Ok, HealthStatus.Ok, health.CheckedAtUtc));
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    {
        logger.LogWarning(ex, "Database health check failed");
        return Results.Json(
            new HealthResponse(HealthStatus.Ok, HealthStatus.Unreachable, null),
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapHostApi().MapCourseEndpoints().MapMasterclassEndpoints().MapResultEndpoints();
app.MapParticipantEndpoints();
app.MapRecapEndpoints();
app.MapHub<QuizHub>(QuizHub.Path);

app.Run();
return 0;

// The "Quiz" section of configuration (appsettings.json, or e.g. Quiz__TimeLimitSeconds__Easy=5).
static QuizSettings QuizSettingsFrom(IConfiguration configuration)
{
    var settings = configuration.GetSection(QuizSettings.SectionName).Get<QuizSettings>() ?? new QuizSettings();
    foreach (var difficulty in Enum.GetValues<Difficulty>())
    {
        if (settings.TimeLimitSeconds.MillisecondsFor(difficulty) <= 0)
        {
            throw new InvalidOperationException($"Quiz:TimeLimitSeconds:{difficulty} must be more than 0.");
        }
    }
    if (settings.RevealPauseMilliseconds < 0)
    {
        throw new InvalidOperationException("Quiz:RevealPauseSeconds can't be negative.");
    }
    return settings;
}

internal sealed record HealthResponse(HealthStatus Backend, HealthStatus Database, DateTime? DatabaseCheckedAtUtc);

/// <summary>Serialized as the lowercase strings the frontend's health page matches on.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HealthStatus>))]
internal enum HealthStatus
{
    [JsonStringEnumMemberName("ok")] Ok,
    [JsonStringEnumMemberName("unreachable")] Unreachable,
}
