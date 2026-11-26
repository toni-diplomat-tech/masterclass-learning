using MasterclassQuiz.Api.Database;
using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace MasterclassQuiz.Tests;

/// <summary>
/// The live Masterclass module for one test: the real Quiz engine on the test SQL Server, a fake clock
/// shared by the engine and the module, and the recording broadcast adapter. Dispose it at the end of the
/// test to stop the module's background work, as the app stopping would.
/// </summary>
public sealed class LiveMasterclassFixture : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Short timings for running Sessions: Easy Questions have 3 seconds to answer, so Answers close
    /// <see cref="AnswersClose"/> after the start (with grace); a revealed Question stays up <see cref="RevealPause"/>.
    /// </summary>
    public static readonly QuizSettings ShortTimings = new()
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3 },
        RevealPauseSeconds = 2,
    };

    /// <summary>When an Easy Question's Answers close under <see cref="ShortTimings"/>: 3 s plus the grace.</summary>
    public static readonly TimeSpan AnswersClose = TimeSpan.FromMilliseconds(3_500);

    /// <summary>How long a revealed Question stays up under <see cref="ShortTimings"/>.</summary>
    public static readonly TimeSpan RevealPause = TimeSpan.FromSeconds(2);

    private readonly TestLifetime _lifetime = new();

    /// <param name="settings">The Quiz settings; <see cref="ShortTimings"/> by default.</param>
    /// <param name="database">Another database on the test server to use instead of the shared one.</param>
    public LiveMasterclassFixture(SqlServerFixture sqlServer, QuizSettings? settings = null, DatabaseConnection? database = null)
    {
        Engine = new QuizEngine(database ?? sqlServer.Connection, Time, settings: settings ?? ShortTimings);
        Live = new LiveMasterclass(Engine, Broadcasts, Time, _lifetime, NullLogger<LiveMasterclass>.Instance);
    }

    /// <summary>
    /// The clock of both the Quiz engine and the module; advance it to move time on. To move the backend
    /// timer on, use <see cref="TimerClock.AdvanceWhenTimerWaitsAsync"/> rather than advancing blindly.
    /// </summary>
    public TimerClock Time { get; } = new(Start);

    /// <summary>The Quiz engine the module calls, for arranging a Masterclass and reading state back.</summary>
    public QuizEngine Engine { get; }

    /// <summary>Everything the module broadcast, in order.</summary>
    public RecordedBroadcasts Broadcasts { get; } = new();

    /// <summary>The module under test.</summary>
    public LiveMasterclass Live { get; }

    /// <summary>A new Open Masterclass of a freshly imported example Course, with nobody joined.</summary>
    public async Task<MasterclassOverview> OpenMasterclassAsync()
    {
        var import = await Engine.ImportCourseAsync(ImportFiles.Example(ImportFiles.UniqueSlug()).ToJsonString());
        Assert.Empty(import.Problems);
        var created = await Engine.CreateMasterclassAsync(import.Outcome!.CourseId, "Dev team 2026");
        return await Engine.OpenAsOnlyOpenMasterclassAsync(created.Masterclass!.Id);
    }

    public void Dispose() => _lifetime.Stop();

    // Only ApplicationStopping matters to the module.
    private sealed class TestLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => Stop();

        public void Stop() => _stopping.Cancel();
    }
}
