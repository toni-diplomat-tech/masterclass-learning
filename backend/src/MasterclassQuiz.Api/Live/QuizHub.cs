using MasterclassQuiz.Api.Participants;
using MasterclassQuiz.Api.Quiz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MasterclassQuiz.Api.Live;

/// <summary>
/// The live meeting, at <see cref="Path"/>. Each Masterclass has one group (<see cref="MasterclassGroup"/>)
/// that its broadcasts go to. Like the REST endpoints, the hub is a thin caller of the Quiz engine.
/// </summary>
public sealed class QuizHub(QuizEngine engine, SessionNotifier sessions) : Hub
{
    public const string Path = "/hubs/quiz";

    /// <summary>Server → clients: <c>{ masterclassId, participants }</c> whenever someone joins.</summary>
    public const string ParticipantCountUpdated = nameof(ParticipantCountUpdated);

    /// <summary>
    /// Server → clients: a <see cref="Quiz.QuestionStarted"/> (text, options, UTC start and deadline, never the
    /// correct option) when a Question starts.
    /// </summary>
    public const string QuestionStarted = nameof(QuestionStarted);

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

    /// <summary>
    /// A Participant's phone: follow their Masterclass's broadcasts, found by the browser's participant
    /// cookie. Returns the Participant; null (and following nothing) without a known cookie.
    /// </summary>
    public async Task<Participant?> WatchAsParticipant()
    {
        var token = Context.GetHttpContext()?.Request.Cookies[ParticipantEndpoints.CookieName];
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }
        var participant = await engine.GetParticipantAsync(token, Context.ConnectionAborted);
        if (participant is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, MasterclassGroup(participant.MasterclassId), Context.ConnectionAborted);
        }
        return participant;
    }

    /// <summary>
    /// Host only: start a Workshop's Session with a Difficulty mix. Its first Question goes out to the
    /// Masterclass as <see cref="QuestionStarted"/>. Replies with every reason it was refused (empty: started).
    /// </summary>
    [Authorize]
    public async Task<SessionStartReply> StartSession(int masterclassId, int workshopId, DifficultyCounts mix)
    {
        var result = await engine.StartSessionAsync(masterclassId, workshopId, mix, Context.ConnectionAborted);
        if (result.Session is { } session)
        {
            await sessions.QuestionStartedAsync(session.FirstQuestion);
        }
        return new SessionStartReply(result.Problems);
    }
}

public sealed record SessionStartReply(IReadOnlyList<string> Problems);

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

/// <summary>Session broadcasts to a Masterclass's group, from the hub or from outside it (e.g. timers).</summary>
public sealed class SessionNotifier(IHubContext<QuizHub> hub)
{
    public Task QuestionStartedAsync(QuestionStarted question, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(QuizHub.MasterclassGroup(question.MasterclassId)).SendAsync(QuizHub.QuestionStarted, question, cancellationToken);
}
