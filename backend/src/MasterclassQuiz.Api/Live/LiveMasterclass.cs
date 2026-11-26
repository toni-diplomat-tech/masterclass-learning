using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Live;

/// <summary>
/// The live Masterclass module: every change to a live Masterclass goes through it. Each operation makes
/// its Quiz engine call and sends the broadcasts that go with the change, through the broadcast port
/// (<see cref="IMasterclassBroadcasts"/>). It adds no business rules: the engine decides.
/// Once the engine has made a change, its broadcasts go out with the application's lifetime, not the
/// caller's cancellation, so a caller going away can't leave the screens behind.
/// The backend timer that reveals and advances running Sessions is internal to it and shares the
/// engine's <see cref="TimeProvider"/>, so a fake clock moves both.
/// </summary>
public sealed class LiveMasterclass(
    QuizEngine engine,
    IMasterclassBroadcasts broadcasts,
    TimeProvider timeProvider,
    IHostApplicationLifetime lifetime,
    ILogger<LiveMasterclass> logger)
{
    private readonly SessionRunner _timer = new(engine, broadcasts, timeProvider, lifetime, logger);

    /// <summary>
    /// Starts a Workshop's Session with a Difficulty mix, broadcasts its first Question
    /// (<see cref="QuizHub.QuestionStarted"/>) and has the backend timer follow it. Returns every reason the
    /// start was refused; empty when the Session started. A refused start broadcasts nothing.
    /// </summary>
    public async Task<IReadOnlyList<string>> StartSessionAsync(int masterclassId, int workshopId, DifficultyCounts mix, CancellationToken cancellationToken = default)
    {
        var result = await engine.StartSessionAsync(masterclassId, workshopId, mix, cancellationToken);
        if (result.Session is { } session)
        {
            await broadcasts.QuestionStartedAsync(session.FirstQuestion, lifetime.ApplicationStopping);
            _timer.Follow(session.Id);
        }
        return result.Problems;
    }

    /// <summary>
    /// Resumes the Masterclass's running Sessions (the projector's Resume button): the backend timer follows
    /// each one and checks it now, so an overdue Question is revealed and the Session carries on. A Session
    /// the timer already follows is only checked again, so resuming twice broadcasts nothing twice.
    /// Returns the Sessions resumed; none once they have ended.
    /// </summary>
    public Task<IReadOnlyList<int>> ResumeMasterclassAsync(int masterclassId, CancellationToken cancellationToken = default) =>
        ResumeAsync(masterclassId, cancellationToken);

    /// <summary>
    /// Resumes every running Session, of every Masterclass (on startup, after a restart), as
    /// <see cref="ResumeMasterclassAsync"/> does for one. Returns the Sessions resumed.
    /// </summary>
    public Task<IReadOnlyList<int>> ResumeAllSessionsAsync(CancellationToken cancellationToken = default) =>
        ResumeAsync(null, cancellationToken);

    private async Task<IReadOnlyList<int>> ResumeAsync(int? masterclassId, CancellationToken cancellationToken)
    {
        var sessions = await engine.ListRunningSessionsAsync(masterclassId, cancellationToken);
        foreach (var sessionId in sessions)
        {
            _timer.Follow(sessionId);
            _timer.Wake(sessionId);
        }
        return sessions;
    }

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

    /// <summary>
    /// Joins the Open Masterclass with this Join code (or gives a browser whose
    /// <paramref name="currentToken"/> already joined it its Participant back) and broadcasts
    /// <see cref="QuizHub.ParticipantCountUpdated"/> with the Masterclass's new Participant count.
    /// Null, broadcasting nothing, when no Open Masterclass has this Join code.
    /// </summary>
    public async Task<ParticipantJoin?> JoinMasterclassAsync(string joinCode, string? currentToken, CancellationToken cancellationToken = default)
    {
        var joined = await engine.JoinMasterclassAsync(joinCode, currentToken, cancellationToken);
        if (joined is null)
        {
            return null;
        }
        var masterclassId = joined.Participant.MasterclassId;
        if (await engine.GetMasterclassAsync(masterclassId, lifetime.ApplicationStopping) is { } masterclass)
        {
            await broadcasts.ParticipantCountUpdatedAsync(new ParticipantCount(masterclassId, masterclass.Participants), lifetime.ApplicationStopping);
        }
        return joined;
    }

    /// <summary>
    /// The Participant whose token this is answers a running Question with the option at
    /// <paramref name="option"/>. An accepted Answer broadcasts <see cref="QuizHub.AnswerCountUpdated"/>;
    /// when it was the last one the Question was waiting for, the backend timer reveals the Question now
    /// instead of at its deadline. Returns the Answer result, never whether the Answer is correct; a refused Answer broadcasts nothing.
    /// </summary>
    public async Task<AnswerResult> SubmitAnswerAsync(string participantToken, int questionId, int option, CancellationToken cancellationToken = default)
    {
        var submitted = await engine.SubmitAnswerAsync(participantToken, questionId, option, cancellationToken);
        if (submitted is { Result: AnswerResult.Accepted, Count: { } count })
        {
            await broadcasts.AnswerCountUpdatedAsync(count, lifetime.ApplicationStopping);
            if (count.EveryoneAnswered)
            {
                _timer.Wake(count.SessionId);
            }
        }
        return submitted.Result;
    }
}
