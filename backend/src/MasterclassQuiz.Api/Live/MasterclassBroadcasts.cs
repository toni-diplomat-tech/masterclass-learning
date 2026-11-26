using MasterclassQuiz.Api.Quiz;
using Microsoft.AspNetCore.SignalR;

namespace MasterclassQuiz.Api.Live;

/// <summary>
/// The broadcast port: the one way broadcasts leave the <see cref="LiveMasterclass"/> module. One method per
/// broadcast, each addressed to the Masterclass its payload belongs to. In production the
/// <see cref="SignalRBroadcasts"/> adapter sends them to the Masterclass's group; tests record them.
/// </summary>
public interface IMasterclassBroadcasts
{
    /// <summary><see cref="QuizHub.ParticipantCountUpdated"/>: someone joined.</summary>
    Task ParticipantCountUpdatedAsync(ParticipantCount count, CancellationToken cancellationToken = default);

    /// <summary><see cref="QuizHub.QuestionStarted"/>: a Session started, or moved on to its next Question.</summary>
    Task QuestionStartedAsync(QuestionStarted question, CancellationToken cancellationToken = default);

    /// <summary><see cref="QuizHub.AnswerCountUpdated"/>: an Answer was accepted.</summary>
    Task AnswerCountUpdatedAsync(AnswerCount count, CancellationToken cancellationToken = default);

    /// <summary><see cref="QuizHub.QuestionRevealed"/>: the running Question was revealed.</summary>
    Task QuestionRevealedAsync(QuestionRevealed revealed, CancellationToken cancellationToken = default);

    /// <summary><see cref="QuizHub.SessionEnded"/>: a Session ended after its last reveal.</summary>
    Task SessionEndedAsync(SessionEnded ended, CancellationToken cancellationToken = default);

    /// <summary><see cref="QuizHub.LeaderboardUpdated"/>: the Session leaderboard after a reveal or the end.</summary>
    Task LeaderboardUpdatedAsync(SessionLeaderboard leaderboard, CancellationToken cancellationToken = default);

    /// <summary><see cref="QuizHub.MasterclassClosed"/>: the Host closed the Masterclass.</summary>
    Task MasterclassClosedAsync(int masterclassId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The broadcast port's production adapter: sends each broadcast to the Masterclass's SignalR group
/// (<see cref="QuizHub.MasterclassGroup"/>) under the hub's message names.
/// </summary>
public sealed class SignalRBroadcasts(IHubContext<QuizHub> hub) : IMasterclassBroadcasts
{
    public Task ParticipantCountUpdatedAsync(ParticipantCount count, CancellationToken cancellationToken = default) =>
        Send(count.MasterclassId, QuizHub.ParticipantCountUpdated, count, cancellationToken);

    public Task QuestionStartedAsync(QuestionStarted question, CancellationToken cancellationToken = default) =>
        Send(question.MasterclassId, QuizHub.QuestionStarted, question, cancellationToken);

    public Task AnswerCountUpdatedAsync(AnswerCount count, CancellationToken cancellationToken = default) =>
        Send(count.MasterclassId, QuizHub.AnswerCountUpdated, count, cancellationToken);

    public Task QuestionRevealedAsync(QuestionRevealed revealed, CancellationToken cancellationToken = default) =>
        Send(revealed.MasterclassId, QuizHub.QuestionRevealed, revealed, cancellationToken);

    public Task SessionEndedAsync(SessionEnded ended, CancellationToken cancellationToken = default) =>
        Send(ended.MasterclassId, QuizHub.SessionEnded, ended, cancellationToken);

    public Task LeaderboardUpdatedAsync(SessionLeaderboard leaderboard, CancellationToken cancellationToken = default) =>
        Send(leaderboard.MasterclassId, QuizHub.LeaderboardUpdated, leaderboard, cancellationToken);

    public Task MasterclassClosedAsync(int masterclassId, CancellationToken cancellationToken = default) =>
        Send(masterclassId, QuizHub.MasterclassClosed, new MasterclassClosedMessage(masterclassId), cancellationToken);

    private Task Send(int masterclassId, string message, object payload, CancellationToken cancellationToken) =>
        hub.Clients.Group(QuizHub.MasterclassGroup(masterclassId)).SendAsync(message, payload, cancellationToken);
}

public sealed record MasterclassClosedMessage(int MasterclassId);
