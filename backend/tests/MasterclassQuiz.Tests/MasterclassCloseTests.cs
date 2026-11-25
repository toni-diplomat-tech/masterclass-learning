using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>
/// Closing a Masterclass at the end of the meeting: no more joining or Sessions, its Join code is free
/// again, and Recaps stay readable.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class MasterclassCloseTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private QuizEngine Engine => new(sqlServer.Connection, _time, settings: new QuizSettings
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3, Medium = 3, Hard = 3 },
        RevealPauseSeconds = 0,
    });

    [Fact]
    public async Task Closing_an_open_masterclass_makes_it_closed()
    {
        var opened = await OpenMasterclassAsync();
        _time.Advance(TimeSpan.FromHours(2));

        var closed = await Engine.CloseMasterclassAsync(opened.Id);

        Assert.Empty(closed.Problems);
        Assert.Equal((MasterclassStatus.Closed, Now.AddHours(2).UtcDateTime, opened.JoinCode), (closed.Masterclass!.Status, closed.Masterclass.ClosedAt, closed.Masterclass.JoinCode));
        Assert.Equal(MasterclassStatus.Closed, (await Engine.GetMasterclassAsync(opened.Id))!.Status);
    }

    private async Task<MasterclassOverview> OpenMasterclassAsync()
    {
        var import = await Engine.ImportCourseAsync(ImportFiles.Example(ImportFiles.UniqueSlug()).ToJsonString());
        var created = await Engine.CreateMasterclassAsync(import.Summary!.CourseId, "Dev team 2026");
        return (await Engine.OpenMasterclassAsync(created.Masterclass!.Id))!;
    }
}
