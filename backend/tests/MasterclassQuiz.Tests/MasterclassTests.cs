using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class MasterclassTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly QuizEngine _engine = new(sqlServer.Connection, new FakeTimeProvider(Now));

    [Fact]
    public async Task A_new_masterclass_is_a_draft_without_a_join_code()
    {
        var course = await ImportCourseAsync();

        var created = await _engine.CreateMasterclassAsync(course.CourseId, "Dev team 2026");

        Assert.Empty(created.Problems);
        var masterclass = await _engine.GetMasterclassAsync(created.Masterclass!.Id);
        Assert.NotNull(masterclass);
        Assert.Equal(
            ("Dev team 2026", course.CourseId, course.CourseTitle, MasterclassStatus.Draft, (string?)null, 0, Now.UtcDateTime),
            (masterclass.Name, masterclass.CourseId, masterclass.CourseTitle, masterclass.Status, masterclass.JoinCode, masterclass.Participants, masterclass.CreatedAt));
        Assert.Contains(await _engine.ListMasterclassesAsync(), m => m.Id == masterclass.Id);
    }

    [Fact]
    public async Task A_masterclass_needs_a_name_and_an_existing_course()
    {
        var created = await _engine.CreateMasterclassAsync(courseId: -1, "  ");

        Assert.Null(created.Masterclass);
        Assert.Equal(["The Masterclass needs a name.", "There is no such Course."], created.Problems);
    }

    [Fact]
    public async Task Opening_a_masterclass_gives_it_a_six_digit_join_code()
    {
        var draft = await CreateMasterclassAsync();

        var opened = await _engine.OpenMasterclassAsync(draft.Id);

        Assert.NotNull(opened);
        Assert.Equal((MasterclassStatus.Open, Now.UtcDateTime), (opened.Status, opened.OpenedAt));
        Assert.Matches("^[0-9]{6}$", opened.JoinCode);
    }

    [Fact]
    public async Task Opening_an_open_masterclass_again_keeps_its_join_code()
    {
        var first = await _engine.OpenMasterclassAsync((await CreateMasterclassAsync()).Id);

        var again = await _engine.OpenMasterclassAsync(first!.Id);

        Assert.Equal((MasterclassStatus.Open, first.JoinCode), (again!.Status, again.JoinCode));
    }

    [Fact]
    public async Task Open_masterclasses_never_share_a_join_code()
    {
        // Engines with the same seed draw the same codes, so every Masterclass after the first one
        // draws a code that is already taken and has to look further.
        var openCodes = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var engine = new QuizEngine(sqlServer.Connection, new FakeTimeProvider(Now), new Random(42));
            var opened = await engine.OpenMasterclassAsync((await CreateMasterclassAsync()).Id);
            openCodes.Add(opened!.JoinCode!);
        }

        Assert.Equal(openCodes.Count, openCodes.Distinct().Count());
    }

    [Fact]
    public async Task Opening_an_unknown_masterclass_finds_nothing()
    {
        Assert.Null(await _engine.OpenMasterclassAsync(-1));
    }

    private async Task<MasterclassOverview> CreateMasterclassAsync()
    {
        var course = await ImportCourseAsync();
        return (await _engine.CreateMasterclassAsync(course.CourseId, "Dev team 2026")).Masterclass!;
    }

    private async Task<CourseImportSummary> ImportCourseAsync()
    {
        var result = await _engine.ImportCourseAsync(ImportFiles.Example(ImportFiles.UniqueSlug()).ToJsonString());
        return result.Summary!;
    }
}
