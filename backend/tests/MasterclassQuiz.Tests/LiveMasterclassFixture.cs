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

    private readonly TestLifetime _lifetime = new();

    public LiveMasterclassFixture(SqlServerFixture sqlServer, QuizSettings? settings = null)
    {
        Engine = new QuizEngine(sqlServer.Connection, Time, settings: settings);
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
        return (await Engine.OpenMasterclassAsync(created.Masterclass!.Id))!;
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
