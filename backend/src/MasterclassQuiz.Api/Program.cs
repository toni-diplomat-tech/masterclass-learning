using MasterclassQuiz.Api.Database;
using MasterclassQuiz.Api.Quiz;

var builder = WebApplication.CreateBuilder(args);

var database = new DatabaseConnection(
    builder.Configuration.GetConnectionString("Quiz")
    ?? throw new InvalidOperationException("Connection string 'Quiz' is not set."));

builder.Services.AddSingleton(database);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<QuizEngine>();

var app = builder.Build();

DatabaseMigrator.Migrate(database.ConnectionString, DatabaseMigrator.DefaultScriptsDirectory);

app.MapGet("/health", async (QuizEngine engine, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    try
    {
        var health = await engine.CheckHealthAsync(cancellationToken);
        return Results.Ok(new HealthResponse("ok", "ok", health.CheckedAtUtc));
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogWarning(ex, "Database health check failed");
        return Results.Json(new HealthResponse("ok", "unreachable", null), statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

internal sealed record HealthResponse(string Backend, string Database, DateTime? DatabaseCheckedAtUtc);
