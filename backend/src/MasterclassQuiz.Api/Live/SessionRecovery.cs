using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Live;

/// <summary>
/// Restart recovery: on startup, the backend timer follows every Session that hasn't ended, so a Question
/// whose deadline passed while the backend was down is revealed (with Timed out outcomes) and the Session
/// carries on. Retries until the database answers. Configuration <see cref="EnabledKey"/> (default true)
/// turns it off, e.g. for tests that share one database.
/// </summary>
public sealed class SessionRecovery(QuizEngine engine, LiveMasterclass live, ILogger<SessionRecovery> logger) : BackgroundService
{
    public const string EnabledKey = "Live:ResumeSessionsOnStartup";

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sessions = await engine.ListRunningSessionsAsync(cancellationToken: stoppingToken);
                foreach (var sessionId in sessions)
                {
                    live.FollowSession(sessionId);
                }
                if (sessions.Count > 0)
                {
                    logger.LogInformation("Resumed {Count} running Session(s): {SessionIds}", sessions.Count, sessions);
                }
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Could not list the running Sessions to resume; retrying");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }
}
