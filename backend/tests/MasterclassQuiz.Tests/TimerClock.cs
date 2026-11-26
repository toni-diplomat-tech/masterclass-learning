using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that knows when someone is waiting on it, so a test can move background
/// work (the backend timer) on deterministically. The timer runs in the background: it reads the clock,
/// then starts a delay until the next step is due. Advancing the clock in between would leave that delay
/// counting from the new time, and the step would never come. <see cref="AdvanceWhenTimerWaitsAsync"/>
/// first waits until a delay is pending on this clock (the timer is parked), then advances.
/// </summary>
public sealed class TimerClock(DateTimeOffset start) : FakeTimeProvider(start)
{
    /// <summary>How long <see cref="WaitForTimerAsync"/> waits by default before failing the test.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    private readonly Lock _gate = new();
    private readonly HashSet<WatchedTimer> _pending = [];
    private TaskCompletionSource _parked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Waits until some delay on this clock is pending (started, and not yet fired, cancelled or disposed),
    /// e.g. the backend timer waiting for a deadline or the reveal pause. Any pending delay counts, including
    /// the timer's short re-check after a step that wasn't due.
    /// </summary>
    public async Task WaitForTimerAsync(TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        Task parked;
        lock (_gate)
        {
            if (_pending.Count > 0)
            {
                return;
            }
            parked = _parked.Task;
        }
        try
        {
            await parked.WaitAsync(limit);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"Nothing waited on the clock within {limit.TotalMilliseconds} ms.");
        }
    }

    /// <summary>Once a delay is pending (<see cref="WaitForTimerAsync"/>), advances the clock by <paramref name="by"/>.</summary>
    public async Task AdvanceWhenTimerWaitsAsync(TimeSpan by, TimeSpan? timeout = null)
    {
        await WaitForTimerAsync(timeout);
        Advance(by);
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var watched = new WatchedTimer(this);
        // Counted as pending only once the fake timer exists, so an Advance can't slip in before it.
        watched.Inner = base.CreateTimer(s =>
        {
            watched.Fired();
            callback(s);
        }, state, dueTime, period);
        watched.Started(dueTime, period);
        return watched;
    }

    private sealed class WatchedTimer(TimerClock clock) : ITimer
    {
        private bool _oneShot;
        private bool _fired;

        public ITimer? Inner { get; set; }

        // Pending while due, unless a one-shot timer has already fired (it may fire as it is created).
        public void Started(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                _oneShot = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero;
                if (dueTime == Timeout.InfiniteTimeSpan || (_oneShot && _fired))
                {
                    clock._pending.Remove(this);
                }
                else if (clock._pending.Add(this))
                {
                    clock._parked.TrySetResult();
                    clock._parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
        }

        public void Fired()
        {
            lock (clock._gate)
            {
                _fired = true;
                if (_oneShot)
                {
                    clock._pending.Remove(this);
                }
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                _fired = false;
            }
            var changed = Inner!.Change(dueTime, period);
            Started(dueTime, period);
            return changed;
        }

        public void Dispose()
        {
            lock (clock._gate)
            {
                clock._pending.Remove(this);
            }
            Inner?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
