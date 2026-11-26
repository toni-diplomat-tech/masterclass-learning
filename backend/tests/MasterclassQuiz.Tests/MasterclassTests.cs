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
    public async Task Opening_a_masterclass_while_none_is_open_gives_it_a_six_digit_join_code()
    {
        var draft = await CreateMasterclassAsync();
        await OnlyOpenMasterclass.CloseOpenMasterclassesAsync(sqlServer.Connection, new FakeTimeProvider(Now));

        var opened = await _engine.OpenMasterclassAsync(draft.Id);

        Assert.Empty(opened.Problems);
        Assert.Equal((MasterclassStatus.Open, Now.UtcDateTime), (opened.Masterclass!.Status, opened.Masterclass.OpenedAt));
        Assert.Matches("^[0-9]{6}$", opened.Masterclass.JoinCode);
    }

    [Fact]
    public async Task Opening_the_open_masterclass_again_keeps_its_join_code()
    {
        var first = await _engine.OpenAsOnlyOpenMasterclassAsync((await CreateMasterclassAsync()).Id, sqlServer.Connection, new FakeTimeProvider(Now));

        var again = await _engine.OpenMasterclassAsync(first.Id);

        Assert.Empty(again.Problems);
        Assert.Equal((MasterclassStatus.Open, first.JoinCode), (again.Masterclass!.Status, again.Masterclass.JoinCode));
    }

    [Fact]
    public async Task A_draft_can_not_open_while_another_masterclass_is_open()
    {
        var open = await _engine.OpenAsOnlyOpenMasterclassAsync((await CreateMasterclassAsync("Dev team 2026")).Id, sqlServer.Connection, new FakeTimeProvider(Now));
        var draft = await CreateMasterclassAsync("QA team 2026");

        var refused = await _engine.OpenMasterclassAsync(draft.Id);

        Assert.Equal(["Close \"Dev team 2026\" first."], refused.Problems);
        Assert.Equal((MasterclassStatus.Draft, (string?)null), (refused.Masterclass!.Status, refused.Masterclass.JoinCode));
        Assert.Equal(MasterclassStatus.Open, (await _engine.GetMasterclassAsync(open.Id))!.Status);
    }

    [Fact]
    public async Task A_draft_opens_once_the_open_masterclass_is_closed()
    {
        var open = await _engine.OpenAsOnlyOpenMasterclassAsync((await CreateMasterclassAsync("Dev team 2026")).Id, sqlServer.Connection, new FakeTimeProvider(Now));
        var draft = await CreateMasterclassAsync("QA team 2026");
        await _engine.OpenMasterclassAsync(draft.Id);

        await _engine.CloseMasterclassAsync(open.Id);
        var opened = await _engine.OpenMasterclassAsync(draft.Id);

        Assert.Empty(opened.Problems);
        Assert.Equal(MasterclassStatus.Open, opened.Masterclass!.Status);
        Assert.Matches("^[0-9]{6}$", opened.Masterclass.JoinCode);
    }

    [Fact]
    public async Task Drafts_opening_at_the_same_moment_can_not_both_open()
    {
        // Two openings seldom interleave, so several rounds of many Hosts each, released together.
        const int Rounds = 5, Hosts = 12;
        var course = await ImportCourseAsync();
        for (var round = 0; round < Rounds; round++)
        {
            var drafts = new List<MasterclassOverview>();
            for (var i = 0; i < Hosts; i++)
            {
                drafts.Add((await _engine.CreateMasterclassAsync(course.CourseId, $"Team {i}")).Masterclass!);
            }
            await OnlyOpenMasterclass.CloseOpenMasterclassesAsync(sqlServer.Connection, new FakeTimeProvider(Now));

            var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var openings = drafts.Select(draft => Task.Run(async () =>
            {
                await go.Task;
                return await new QuizEngine(sqlServer.Connection, new FakeTimeProvider(Now)).OpenMasterclassAsync(draft.Id);
            })).ToList();
            go.SetResult();
            var results = await Task.WhenAll(openings);

            Assert.Single(results, result => result.Succeeded);
            Assert.All(results.Where(result => !result.Succeeded), refused => Assert.Equal(MasterclassStatus.Draft, refused.Masterclass!.Status));
            Assert.Single(await _engine.ListMasterclassesAsync(), masterclass => masterclass.Status == MasterclassStatus.Open);
        }
    }

    [Fact]
    public async Task A_closed_masterclass_can_not_open_again()
    {
        var closed = await _engine.OpenAsOnlyOpenMasterclassAsync((await CreateMasterclassAsync()).Id, sqlServer.Connection, new FakeTimeProvider(Now));
        await _engine.CloseMasterclassAsync(closed.Id);

        var again = await _engine.OpenMasterclassAsync(closed.Id);

        Assert.Equal(["A Closed Masterclass can't be opened again."], again.Problems);
        Assert.Equal(MasterclassStatus.Closed, again.Masterclass!.Status);
    }

    [Fact]
    public async Task Opening_an_unknown_masterclass_finds_nothing()
    {
        Assert.Null((await _engine.OpenMasterclassAsync(-1)).Masterclass);
    }

    private async Task<MasterclassOverview> CreateMasterclassAsync(string name = "Dev team 2026")
    {
        var course = await ImportCourseAsync();
        return (await _engine.CreateMasterclassAsync(course.CourseId, name)).Masterclass!;
    }

    private async Task<CourseImportOutcome> ImportCourseAsync()
    {
        var result = await _engine.ImportCourseAsync(ImportFiles.Example(ImportFiles.UniqueSlug()).ToJsonString());
        return result.Outcome!;
    }
}
