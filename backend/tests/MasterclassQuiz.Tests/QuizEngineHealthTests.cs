using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class QuizEngineHealthTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task Health_check_reports_the_time_from_the_engine_clock()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 30, 15, 123, TimeSpan.Zero));
        var engine = new QuizEngine(sqlServer.Connection, clock);

        var first = await engine.CheckHealthAsync();
        clock.Advance(TimeSpan.FromSeconds(11));
        var second = await engine.CheckHealthAsync();

        Assert.Equal(new DateTime(2026, 3, 1, 9, 30, 15, 123, DateTimeKind.Utc), first.CheckedAtUtc);
        Assert.Equal(new DateTime(2026, 3, 1, 9, 30, 26, 123, DateTimeKind.Utc), second.CheckedAtUtc);
    }
}
