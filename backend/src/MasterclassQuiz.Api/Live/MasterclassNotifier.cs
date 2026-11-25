using Microsoft.AspNetCore.SignalR;

namespace MasterclassQuiz.Api.Live;

/// <summary>Masterclass broadcasts sent from outside the hub, e.g. after the Host closes it over REST.</summary>
public sealed class MasterclassNotifier(IHubContext<QuizHub> hub)
{
    /// <summary>Server → clients: <c>{ masterclassId }</c> once the Host has closed the Masterclass.</summary>
    public const string MasterclassClosed = nameof(MasterclassClosed);

    public Task MasterclassClosedAsync(int masterclassId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(QuizHub.MasterclassGroup(masterclassId)).SendAsync(
            MasterclassClosed, new MasterclassClosedMessage(masterclassId), cancellationToken);
}

public sealed record MasterclassClosedMessage(int MasterclassId);
