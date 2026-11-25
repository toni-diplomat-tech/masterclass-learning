using MasterclassQuiz.Api.Quiz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MasterclassQuiz.Api.Live;

/// <summary>
/// The live meeting, at <see cref="Path"/>. Each Masterclass has one group (<see cref="MasterclassGroup"/>)
/// that its broadcasts go to. Like the REST endpoints, the hub is a thin caller of the Quiz engine.
/// </summary>
public sealed class QuizHub(QuizEngine engine) : Hub
{
    public const string Path = "/hubs/quiz";

    /// <summary>Server → clients: <c>{ masterclassId, participants }</c> whenever someone joins.</summary>
    public const string ParticipantCountUpdated = nameof(ParticipantCountUpdated);

    public static string MasterclassGroup(int masterclassId) => $"masterclass-{masterclassId}";

    /// <summary>
    /// Host only (the projector): follow a Masterclass's broadcasts. Returns the Masterclass as it is now,
    /// so the projector has the current count before any update; null if there is no such Masterclass.
    /// </summary>
    [Authorize]
    public async Task<MasterclassOverview?> WatchMasterclass(int masterclassId)
    {
        var masterclass = await engine.GetMasterclassAsync(masterclassId, Context.ConnectionAborted);
        if (masterclass is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, MasterclassGroup(masterclassId), Context.ConnectionAborted);
        }
        return masterclass;
    }
}

/// <summary>Live updates sent from outside the hub, e.g. after a Participant joins over REST.</summary>
public sealed class ParticipantNotifier(IHubContext<QuizHub> hub, QuizEngine engine)
{
    public async Task ParticipantJoinedAsync(int masterclassId, CancellationToken cancellationToken = default)
    {
        var masterclass = await engine.GetMasterclassAsync(masterclassId, cancellationToken);
        if (masterclass is null)
        {
            return;
        }
        await hub.Clients.Group(QuizHub.MasterclassGroup(masterclassId)).SendAsync(
            QuizHub.ParticipantCountUpdated,
            new ParticipantCount(masterclassId, masterclass.Participants),
            cancellationToken);
    }
}

public sealed record ParticipantCount(int MasterclassId, int Participants);
