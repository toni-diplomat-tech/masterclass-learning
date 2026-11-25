using System.Collections.Concurrent;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Live;

/// <summary>
/// The backend timer that moves running Sessions on: for each followed Session it waits until the next
/// step is due (<see cref="SessionProgress.DueAt"/>), then reveals the running Question or, after the
/// reveal pause, starts the next one or ends the Session, and broadcasts what happened. The Quiz engine
/// decides whether a step is due, so a late or early wake-up does no harm. <see cref="Wake"/> makes it
/// look again before the deadline (once every Participant has answered).
/// </summary>
public sealed class SessionRunner(
    QuizEngine engine,
    SessionNotifier notifier,
    TimeProvider timeProvider,
    IHostApplicationLifetime lifetime,
    ILogger<SessionRunner> logger)
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(50);

    private readonly ConcurrentDictionary<int, SemaphoreSlim> _followed = new();

    /// <summary>
    /// Runs the Session until it ends, in the background. Following a Session that is already followed
    /// does nothing, so it is safe to call again (e.g. when resuming Sessions after a restart).
    /// </summary>
    public void Follow(int sessionId)
    {
        var wake = new SemaphoreSlim(0);
        if (_followed.TryAdd(sessionId, wake))
        {
            _ = Task.Run(() => RunAsync(sessionId, wake, lifetime.ApplicationStopping));
        }
    }

    /// <summary>Makes a followed Session check now whether its running Question can be revealed.</summary>
    public void Wake(int sessionId)
    {
        if (_followed.TryGetValue(sessionId, out var wake))
        {
            wake.Release();
        }
    }

    private async Task RunAsync(int sessionId, SemaphoreSlim wake, CancellationToken stopping)
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                try
                {
                    var progress = await engine.GetSessionProgressAsync(sessionId, stopping);
                    if (progress is null || progress.Step == SessionStep.Ended)
                    {
                        return;
                    }
                    await WaitUntilAsync(progress.DueAt!.Value, progress.Step == SessionStep.QuestionRunning ? wake : null, stopping);
                    if (!await TakeStepAsync(sessionId, progress.Step, stopping))
                    {
                        // Not due yet (woken early, or the clocks differ slightly): look again shortly.
                        await Task.Delay(IdleDelay, timeProvider, stopping);
                    }
                }
                catch (Exception ex) when (!stopping.IsCancellationRequested)
                {
                    logger.LogError(ex, "Session {SessionId} could not move on; retrying", sessionId);
                    await Task.Delay(RetryDelay, timeProvider, stopping);
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
        finally
        {
            _followed.TryRemove(sessionId, out _);
        }
    }

    // Reveals the running Question, or moves on after the pause. False when the engine found nothing due.
    private async Task<bool> TakeStepAsync(int sessionId, SessionStep step, CancellationToken stopping)
    {
        if (step == SessionStep.QuestionRunning)
        {
            var revealed = await engine.RevealQuestionAsync(sessionId, stopping);
            if (revealed is not null)
            {
                await notifier.QuestionRevealedAsync(revealed, stopping);
            }
            return revealed is not null;
        }

        var advance = await engine.AdvanceSessionAsync(sessionId, stopping);
        if (advance.Started is { } started)
        {
            await notifier.QuestionStartedAsync(started, stopping);
        }
        if (advance.Ended is { } ended)
        {
            await notifier.SessionEndedAsync(ended, stopping);
        }
        return advance.Started is not null || advance.Ended is not null;
    }

    // Until dueAt (the server's UTC time), or until woken, whichever comes first.
    private async Task WaitUntilAsync(DateTime dueAt, SemaphoreSlim? wake, CancellationToken stopping)
    {
        var delay = dueAt - timeProvider.GetUtcNow().UtcDateTime;
        if (delay <= TimeSpan.Zero)
        {
            return;
        }
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        var timer = Task.Delay(delay, timeProvider, waiting.Token);
        var woken = wake?.WaitAsync(waiting.Token) ?? Task.Delay(Timeout.InfiniteTimeSpan, waiting.Token);
        await Task.WhenAny(timer, woken);
        await waiting.CancelAsync();
        stopping.ThrowIfCancellationRequested();
    }
}
