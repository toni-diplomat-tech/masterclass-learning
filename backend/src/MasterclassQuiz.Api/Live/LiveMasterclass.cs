using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Live;

/// <summary>
/// The live Masterclass module: every change to a live Masterclass goes through it. Each operation makes
/// its Quiz engine call and sends the broadcasts that go with the change, through the broadcast port
/// (<see cref="IMasterclassBroadcasts"/>). It adds no business rules: the engine decides.
/// Once the engine has made a change, its broadcasts go out with the application's lifetime, not the
/// caller's cancellation, so a caller going away can't leave the screens behind.
/// </summary>
public sealed class LiveMasterclass(QuizEngine engine, IMasterclassBroadcasts broadcasts, IHostApplicationLifetime lifetime)
{
    /// <summary>
    /// Closes the Masterclass and, when this call closed it, broadcasts <see cref="QuizHub.MasterclassClosed"/>
    /// to it. Returns the engine's result: no Masterclass when there is none, problems when the close was
    /// refused. Closing a Closed Masterclass again succeeds but changes nothing, so it broadcasts nothing.
    /// </summary>
    public async Task<MasterclassCloseResult> CloseMasterclassAsync(int masterclassId, CancellationToken cancellationToken = default)
    {
        var before = await engine.GetMasterclassAsync(masterclassId, cancellationToken);
        var result = await engine.CloseMasterclassAsync(masterclassId, cancellationToken);
        if (result.Succeeded && before is not { Status: MasterclassStatus.Closed })
        {
            await broadcasts.MasterclassClosedAsync(masterclassId, lifetime.ApplicationStopping);
        }
        return result;
    }
}
