using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Tests;

/// <summary>Closing a Masterclass through the live Masterclass module tells its whole group.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class LiveMasterclassCloseTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly LiveMasterclassFixture _live = new(sqlServer);

    [Fact]
    public async Task Closing_a_masterclass_broadcasts_that_it_closed()
    {
        var opened = await _live.OpenMasterclassAsync();

        var closed = await _live.Live.CloseMasterclassAsync(opened.Id);

        Assert.Equal(MasterclassStatus.Closed, closed.Masterclass!.Status);
        var message = await _live.Broadcasts.NextAsync<MasterclassClosedMessage>(QuizHub.MasterclassClosed);
        Assert.Equal(opened.Id, message.MasterclassId);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task A_refused_close_broadcasts_nothing()
    {
        var session = await RunningSessionFixture.StartAsync(_live.Engine, _live.Database, _live.Time, participants: 1, easy: 1);

        var closed = await _live.Live.CloseMasterclassAsync(session.MasterclassId);

        Assert.Equal(["A Session is still running; close the Masterclass once it has ended."], closed.Problems);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Closing_a_closed_masterclass_again_broadcasts_nothing()
    {
        var opened = await _live.OpenMasterclassAsync();
        await _live.Live.CloseMasterclassAsync(opened.Id);
        await _live.Broadcasts.NextAsync<MasterclassClosedMessage>(QuizHub.MasterclassClosed);

        var again = await _live.Live.CloseMasterclassAsync(opened.Id);

        Assert.Equal(MasterclassStatus.Closed, again.Masterclass!.Status);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task Closing_an_unknown_masterclass_broadcasts_nothing()
    {
        var closed = await _live.Live.CloseMasterclassAsync(-1);

        Assert.Null(closed.Masterclass);
        await _live.Broadcasts.AssertQuietAsync();
    }

    public void Dispose() => _live.Dispose();
}
