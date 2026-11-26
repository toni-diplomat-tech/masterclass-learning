using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class ParticipantJoinTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 30, 0, TimeSpan.Zero);

    private readonly QuizEngine _engine = new(sqlServer.Connection, new FakeTimeProvider(Now));

    [Fact]
    public async Task Joining_an_open_masterclass_assigns_an_adjective_and_animal_nickname()
    {
        var masterclass = await OpenMasterclassAsync();

        var joined = await _engine.JoinMasterclassAsync(masterclass.JoinCode!, currentToken: null);

        Assert.NotNull(joined);
        Assert.Matches("^[A-Z][a-z]+ [A-Z][a-z]+$", joined.Participant.Nickname);
        Assert.Equal(
            (masterclass.Id, "Dev team 2026", MasterclassStatus.Open, Now.UtcDateTime),
            (joined.Participant.MasterclassId, joined.Participant.MasterclassName, joined.Participant.MasterclassStatus, joined.Participant.JoinedAt));
        Assert.Equal(1, (await _engine.GetMasterclassAsync(masterclass.Id))!.Participants);
    }

    [Fact]
    public async Task Nicknames_are_unique_within_a_masterclass()
    {
        var masterclass = await OpenMasterclassAsync();

        // Engines with the same seed draw the same way, so every join after the first one would pick
        // a taken Nickname if taken ones were not excluded.
        var nicknames = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            var engine = new QuizEngine(sqlServer.Connection, new FakeTimeProvider(Now), new Random(7));
            nicknames.Add((await engine.JoinMasterclassAsync(masterclass.JoinCode!, currentToken: null))!.Participant.Nickname);
        }

        Assert.Equal(nicknames.Count, nicknames.Distinct().Count());
        Assert.Equal(20, (await _engine.GetMasterclassAsync(masterclass.Id))!.Participants);
    }

    [Fact]
    public async Task The_same_nickname_can_be_drawn_in_another_masterclass()
    {
        var first = await OpenMasterclassAsync();
        var inFirst = await new QuizEngine(sqlServer.Connection, new FakeTimeProvider(Now), new Random(7)).JoinMasterclassAsync(first.JoinCode!, null);
        await _engine.CloseMasterclassAsync(first.Id);
        var second = await OpenMasterclassAsync();

        var inSecond = await new QuizEngine(sqlServer.Connection, new FakeTimeProvider(Now), new Random(7)).JoinMasterclassAsync(second.JoinCode!, null);

        Assert.Equal(inFirst!.Participant.Nickname, inSecond!.Participant.Nickname);
        Assert.NotEqual(inFirst.Token, inSecond.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcdef")]
    [InlineData("12345")]
    public async Task Codes_that_are_not_join_codes_are_refused(string joinCode)
    {
        Assert.Null(await _engine.JoinMasterclassAsync(joinCode, currentToken: null));
    }

    [Fact]
    public async Task An_unknown_join_code_is_refused()
    {
        var open = (await _engine.ListMasterclassesAsync())
            .Where(m => m.Status == MasterclassStatus.Open).Select(m => m.JoinCode).ToHashSet();
        var unused = Enumerable.Range(100000, 900000).Select(code => code.ToString()).First(code => !open.Contains(code));

        Assert.Null(await _engine.JoinMasterclassAsync(unused, currentToken: null));
    }

    [Fact]
    public async Task The_token_finds_the_same_participant_again()
    {
        var masterclass = await OpenMasterclassAsync();
        var joined = await _engine.JoinMasterclassAsync(masterclass.JoinCode!, currentToken: null);

        var found = await _engine.GetParticipantAsync(joined!.Token);

        Assert.Equal(joined.Participant, found);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", joined.Token);
    }

    [Fact]
    public async Task An_unknown_token_finds_nobody()
    {
        Assert.Null(await _engine.GetParticipantAsync("not-a-token"));
    }

    [Fact]
    public async Task Joining_again_with_the_current_token_keeps_the_same_participant()
    {
        var masterclass = await OpenMasterclassAsync();
        var first = await _engine.JoinMasterclassAsync(masterclass.JoinCode!, currentToken: null);

        var again = await _engine.JoinMasterclassAsync(masterclass.JoinCode!, first!.Token);

        Assert.Equal(first, again);
        Assert.Equal(1, (await _engine.GetMasterclassAsync(masterclass.Id))!.Participants);
    }

    [Fact]
    public async Task A_token_from_another_masterclass_joins_as_a_new_participant()
    {
        var earlier = await OpenMasterclassAsync();
        var inEarlier = await _engine.JoinMasterclassAsync(earlier.JoinCode!, currentToken: null);
        await _engine.CloseMasterclassAsync(earlier.Id);
        var later = await OpenMasterclassAsync();

        var inLater = await _engine.JoinMasterclassAsync(later.JoinCode!, inEarlier!.Token);

        Assert.Equal(later.Id, inLater!.Participant.MasterclassId);
        Assert.NotEqual(inEarlier.Token, inLater.Token);
        Assert.Equal(inEarlier.Participant with { MasterclassStatus = MasterclassStatus.Closed }, await _engine.GetParticipantAsync(inEarlier.Token));
    }

    private async Task<MasterclassOverview> OpenMasterclassAsync()
    {
        var course = (await _engine.ImportCourseAsync(ImportFiles.Example(ImportFiles.UniqueSlug()).ToJsonString())).Outcome!;
        var draft = (await _engine.CreateMasterclassAsync(course.CourseId, "Dev team 2026")).Masterclass!;
        return await _engine.OpenAsOnlyOpenMasterclassAsync(draft.Id, sqlServer.Connection, new FakeTimeProvider(Now));
    }
}
