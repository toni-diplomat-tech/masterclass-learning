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

    [Fact]
    public async Task Joining_a_closed_masterclass_is_refused()
    {
        var opened = await OpenMasterclassAsync();
        var joined = await Engine.JoinMasterclassAsync(opened.JoinCode!, null);
        await Engine.CloseMasterclassAsync(opened.Id);

        Assert.Null(await Engine.JoinMasterclassAsync(opened.JoinCode!, null));
        Assert.Null(await Engine.JoinMasterclassAsync(opened.JoinCode!, joined!.Token));
    }

    [Fact]
    public async Task No_session_starts_in_a_closed_masterclass()
    {
        var session = await RunOneQuestionSessionAsync();
        await Engine.CloseMasterclassAsync(session.MasterclassId);

        var started = await Engine.StartSessionAsync(session.MasterclassId, await WorkshopIdAsync(session.MasterclassId, "test-doubles"), new DifficultyCounts(1, 0, 0));

        Assert.Equal(["Sessions can only start in an Open Masterclass."], started.Problems);
    }

    [Fact]
    public async Task A_recap_stays_readable_after_the_masterclass_is_closed()
    {
        var session = await RunOneQuestionSessionAsync();
        var recapToken = await Engine.GetRecapTokenAsync(session.Tokens[0]);
        await Engine.CloseMasterclassAsync(session.MasterclassId);

        var recap = await Engine.GetRecapAsync(recapToken!);

        Assert.Equal(MasterclassStatus.Closed, recap!.MasterclassStatus);
        Assert.Equal(RecapOutcome.PerfectScore, Assert.Single(recap.Workshops).Outcome);
        Assert.Equal(recapToken, await Engine.GetRecapTokenAsync(session.Tokens[0]));
    }

    [Fact]
    public async Task Closing_frees_the_join_code_for_another_open_masterclass()
    {
        // Engines with the same seed draw the same codes: the second opening draws the first one's code.
        var first = await OpenMasterclassAsync(new Random(7));
        await Engine.CloseMasterclassAsync(first.Id);

        var second = await OpenMasterclassAsync(new Random(7));

        Assert.Equal(first.JoinCode, second.JoinCode);
        var joined = await Engine.JoinMasterclassAsync(second.JoinCode!, null);
        Assert.Equal(second.Id, joined!.Participant.MasterclassId);
    }

    [Fact]
    public async Task A_draft_masterclass_can_not_be_closed()
    {
        var import = await Engine.ImportCourseAsync(ImportFiles.Example(ImportFiles.UniqueSlug()).ToJsonString());
        var draft = (await Engine.CreateMasterclassAsync(import.Outcome!.CourseId, "Dev team 2026")).Masterclass!;

        var closed = await Engine.CloseMasterclassAsync(draft.Id);

        Assert.Equal(["Only an Open Masterclass can be closed."], closed.Problems);
        Assert.Equal(MasterclassStatus.Draft, closed.Masterclass!.Status);
    }

    [Fact]
    public async Task A_masterclass_can_not_be_closed_while_a_session_runs()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);

        var closed = await Engine.CloseMasterclassAsync(session.MasterclassId);

        Assert.Equal(["A Session is still running; close the Masterclass once it has ended."], closed.Problems);
        Assert.Equal(MasterclassStatus.Open, closed.Masterclass!.Status);
    }

    [Fact]
    public async Task Closing_a_closed_masterclass_again_keeps_it_closed()
    {
        var opened = await OpenMasterclassAsync();
        await Engine.CloseMasterclassAsync(opened.Id);
        _time.Advance(TimeSpan.FromMinutes(5));

        var again = await Engine.CloseMasterclassAsync(opened.Id);

        Assert.Empty(again.Problems);
        Assert.Equal((MasterclassStatus.Closed, Now.UtcDateTime), (again.Masterclass!.Status, again.Masterclass.ClosedAt));
    }

    [Fact]
    public async Task Closing_an_unknown_masterclass_finds_nothing()
    {
        Assert.Null((await Engine.CloseMasterclassAsync(-1)).Masterclass);
    }

    // A Masterclass whose "unit-tests" Session ran its one Easy Question (answered correctly) and ended.
    private async Task<RunningSessionFixture> RunOneQuestionSessionAsync()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        await Engine.RevealQuestionAsync(session.SessionId);
        Assert.NotNull((await Engine.AdvanceSessionAsync(session.SessionId)).Ended);
        return session;
    }

    private async Task<int> WorkshopIdAsync(int masterclassId, string slug)
    {
        var courseId = (await Engine.GetMasterclassAsync(masterclassId))!.CourseId;
        return (await Engine.ListCoursesAsync()).Single(c => c.Id == courseId).Workshops.Single(w => w.Slug == slug).Id;
    }

    private async Task<MasterclassOverview> OpenMasterclassAsync(Random? random = null)
    {
        var engine = new QuizEngine(sqlServer.Connection, _time, random);
        var import = await engine.ImportCourseAsync(ImportFiles.Example(ImportFiles.UniqueSlug()).ToJsonString());
        var created = await engine.CreateMasterclassAsync(import.Outcome!.CourseId, "Dev team 2026");
        return (await engine.OpenMasterclassAsync(created.Masterclass!.Id))!;
    }
}
