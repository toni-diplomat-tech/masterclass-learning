using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Tests;

/// <summary>Joining a Masterclass through the live Masterclass module tells its group the new Participant count.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class LiveMasterclassJoinTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly LiveMasterclassFixture _live = new(sqlServer);

    [Fact]
    public async Task Joining_broadcasts_the_new_participant_count()
    {
        var opened = await _live.OpenMasterclassAsync();

        var joined = await _live.Live.JoinMasterclassAsync(opened.JoinCode!, currentToken: null);

        Assert.NotNull(joined);
        Assert.Equal(opened.Id, joined.Participant.MasterclassId);
        Assert.False(string.IsNullOrEmpty(joined.Token));
        var count = await _live.Broadcasts.NextAsync<ParticipantCount>(QuizHub.ParticipantCountUpdated);
        Assert.Equal(new ParticipantCount(opened.Id, 1), count);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Joining_with_a_bad_join_code_broadcasts_nothing()
    {
        await _live.OpenMasterclassAsync();

        var joined = await _live.Live.JoinMasterclassAsync("NO-SUCH-CODE", currentToken: null);

        Assert.Null(joined);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Joining_a_closed_masterclass_broadcasts_nothing()
    {
        var opened = await _live.OpenMasterclassAsync();
        await _live.Live.CloseMasterclassAsync(opened.Id);
        await _live.Broadcasts.NextAsync<MasterclassClosedMessage>(QuizHub.MasterclassClosed);

        var joined = await _live.Live.JoinMasterclassAsync(opened.JoinCode!, currentToken: null);

        Assert.Null(joined);
        await _live.Broadcasts.AssertQuietAsync();
    }

    public void Dispose() => _live.Dispose();
}
