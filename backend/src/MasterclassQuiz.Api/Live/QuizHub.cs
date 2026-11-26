using MasterclassQuiz.Api.Participants;
using MasterclassQuiz.Api.Quiz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MasterclassQuiz.Api.Live;

/// <summary>
/// The live meeting, at <see cref="Path"/>. Each Masterclass has one group (<see cref="MasterclassGroup"/>)
/// that its broadcasts go to. Like the REST endpoints, the hub is a thin caller of the Quiz engine.
/// </summary>
public sealed partial class QuizHub(QuizEngine engine, SessionNotifier sessions, SessionRunner runner) : Hub
{
    public const string Path = "/hubs/quiz";

    /// <summary>Server → clients: <c>{ masterclassId, participants }</c> whenever someone joins.</summary>
    public const string ParticipantCountUpdated = nameof(ParticipantCountUpdated);

    /// <summary>
    /// Server → clients: a <see cref="Quiz.QuestionStarted"/> (text, options, UTC start and deadline, never the
    /// correct option) when a Question starts.
    /// </summary>
    public const string QuestionStarted = nameof(QuestionStarted);

    /// <summary>
    /// Server → clients: an <see cref="Quiz.AnswerCount"/> (<c>{ masterclassId, sessionId, questionId, answered,
    /// participants }</c>) after each accepted Answer.
    /// </summary>
    public const string AnswerCountUpdated = nameof(AnswerCountUpdated);

    /// <summary>
    /// Server → clients: a <see cref="Quiz.QuestionRevealed"/> (correct option, answers per option, answered and
    /// Timed out counts) when the running Question is revealed.
    /// </summary>
    public const string QuestionRevealed = nameof(QuestionRevealed);

    /// <summary>Server → clients: a <see cref="Quiz.SessionEnded"/> after the pause following the last reveal.</summary>
    public const string SessionEnded = nameof(SessionEnded);

    /// <summary>
    /// Server → clients: a <see cref="Quiz.SessionLeaderboard"/> after each reveal, and once more (final, with
    /// <c>ended</c> true) when the Session ends.
    /// </summary>
    public const string LeaderboardUpdated = nameof(LeaderboardUpdated);

    /// <summary>Server → clients: <c>{ masterclassId }</c> once the Host has closed the Masterclass.</summary>
    public const string MasterclassClosed = nameof(MasterclassClosed);

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
        if (ParticipantToken() is not { } token)
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
            runner.Follow(session.Id);
        }
        return new SessionStartReply(result.Problems);
    }

    /// <summary>
    /// A Participant's phone (known by the participant cookie): answer a running Question with the option at
    /// <paramref name="option"/>. An accepted Answer updates the projector's count; once everyone has
    /// answered, the Question is revealed. Replies with the result only, never whether it is correct.
    /// </summary>
    public async Task<AnswerReply> SubmitAnswer(int questionId, int option)
    {
        if (ParticipantToken() is not { } token)
        {
            return new AnswerReply(AnswerResult.UnknownParticipant);
        }
        var submitted = await engine.SubmitAnswerAsync(token, questionId, option, Context.ConnectionAborted);
        if (submitted is { Result: AnswerResult.Accepted, Count: { } count })
        {
            await sessions.AnswerCountUpdatedAsync(count);
            if (count.EveryoneAnswered)
            {
                runner.Wake(count.SessionId);
            }
        }
        return new AnswerReply(submitted.Result);
    }

    // The caller's participant cookie (set when they joined); null without one.
    private string? ParticipantToken() =>
        Context.GetHttpContext()?.Request.Cookies[ParticipantEndpoints.CookieName] is { Length: > 0 } token ? token : null;
}

public sealed record SessionStartReply(IReadOnlyList<string> Problems);

public sealed record AnswerReply(AnswerResult Result);

public sealed record ParticipantCount(int MasterclassId, int Participants);

/// <summary>Session broadcasts to a Masterclass's group, from the hub or from outside it (e.g. timers).</summary>
public sealed class SessionNotifier(IHubContext<QuizHub> hub)
{
    public Task QuestionStartedAsync(QuestionStarted question, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(QuizHub.MasterclassGroup(question.MasterclassId)).SendAsync(QuizHub.QuestionStarted, question, cancellationToken);

    public Task AnswerCountUpdatedAsync(AnswerCount count, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(QuizHub.MasterclassGroup(count.MasterclassId)).SendAsync(QuizHub.AnswerCountUpdated, count, cancellationToken);

    public Task QuestionRevealedAsync(QuestionRevealed revealed, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(QuizHub.MasterclassGroup(revealed.MasterclassId)).SendAsync(QuizHub.QuestionRevealed, revealed, cancellationToken);

    public Task SessionEndedAsync(SessionEnded ended, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(QuizHub.MasterclassGroup(ended.MasterclassId)).SendAsync(QuizHub.SessionEnded, ended, cancellationToken);

    public Task LeaderboardUpdatedAsync(SessionLeaderboard leaderboard, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(QuizHub.MasterclassGroup(leaderboard.MasterclassId)).SendAsync(QuizHub.LeaderboardUpdated, leaderboard, cancellationToken);
}
