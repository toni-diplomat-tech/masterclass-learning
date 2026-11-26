using Dapper;

namespace MasterclassQuiz.Api.Quiz;

// Sessions: starting a Workshop's Session with a Difficulty mix, and its running Question.
public sealed partial class QuizEngine
{
    /// <summary>
    /// Starts a Workshop's Session in an Open Masterclass: selects <paramref name="mix"/> Questions at random
    /// within each Difficulty (with the fallback rule when a Difficulty runs short), orders them Easy to Hard
    /// and starts the first one, whose deadline is its configured time limit from now. A Workshop gets at
    /// most one Session per Masterclass, and only one Session runs at a time.
    /// </summary>
    public async Task<SessionStartResult> StartSessionAsync(int masterclassId, int workshopId, DifficultyCounts mix, CancellationToken cancellationToken = default)
    {
        var limits = Settings.TimeLimitSeconds;
        var parameters = new DynamicParameters();
        parameters.Add("MasterclassId", masterclassId);
        parameters.Add("WorkshopId", workshopId);
        parameters.Add("Easy", mix.Easy);
        parameters.Add("Medium", mix.Medium);
        parameters.Add("Hard", mix.Hard);
        parameters.Add("EasyLimitMs", limits.MillisecondsFor(Difficulty.Easy));
        parameters.Add("MediumLimitMs", limits.MillisecondsFor(Difficulty.Medium));
        parameters.Add("HardLimitMs", limits.MillisecondsFor(Difficulty.Hard));

        var (problems, rows) = await QueryUnlessProblemsAsync(
            "dbo.StartSession", WithNow(WithSeed(parameters)),
            async results => (await results.ReadAsync<SessionQuestionRow>()).ToList(), cancellationToken);
        if (rows is null)
        {
            return new SessionStartResult(null, problems);
        }

        var sessionId = rows[0].SessionId;
        var firstQuestion = await GetStartedQuestionAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} started without a running Question.");
        return new SessionStartResult(
            new StartedSession(
                sessionId, masterclassId, workshopId,
                rows.Select(row => new SessionQuestion(row.Number, row.QuestionId, row.StableId, (Difficulty)row.Difficulty)).ToList(),
                firstQuestion),
            []);
    }

    /// <summary>The Session's most recently started Question as Participants see it; null if none has started.</summary>
    public async Task<QuestionStarted?> GetStartedQuestionAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SessionId", sessionId);
        return ProcedureJson.Read<QuestionStarted>(await QueryJsonAsync("dbo.GetStartedQuestion", parameters, cancellationToken));
    }

    /// <summary>
    /// Reveals the Session's running Question if it is due: its deadline plus 500 ms grace has passed, or
    /// every Participant who was in the Masterclass when it started has answered. Those without an Answer
    /// are recorded as Timed out. Returns what was revealed; null if nothing was (not due, or no Question
    /// running), so only one caller ever gets a given reveal.
    /// </summary>
    public async Task<QuestionRevealed?> RevealQuestionAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SessionId", sessionId);
        var revealed = await QuerySingleAsync<bool>("dbo.RevealQuestion", WithNow(parameters), cancellationToken);
        return revealed ? await GetRevealedQuestionAsync(sessionId, cancellationToken) : null;
    }

    /// <summary>The Session's most recently revealed Question with its correct option and counts; null if none.</summary>
    public async Task<QuestionRevealed?> GetRevealedQuestionAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SessionId", sessionId);
        return ProcedureJson.Read<QuestionRevealed>(await QueryJsonAsync("dbo.GetRevealedQuestion", parameters, cancellationToken));
    }

    /// <summary>
    /// Moves the Session on once the reveal pause (configuration) after its last reveal has passed: starts
    /// the next Question, or ends the Session after its last one. Ending the last Workshop's Session also
    /// makes the Masterclass Closed, at the same time. Does nothing (both results null) while a Question is
    /// running or the pause lasts, or once the Session has ended.
    /// </summary>
    public async Task<SessionAdvance> AdvanceSessionAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SessionId", sessionId);
        parameters.Add("RevealPauseMs", Settings.RevealPauseMilliseconds);
        var row = await QuerySingleAsync<AdvanceRow>("dbo.AdvanceSession", WithNow(parameters), cancellationToken);
        return row.Advanced switch
        {
            Advanced.Started => new SessionAdvance(await GetStartedQuestionAsync(sessionId, cancellationToken), null),
            Advanced.Ended => new SessionAdvance(null, new SessionEnded(row.MasterclassId!.Value, sessionId, Utc(row.EndedAt!.Value)), row.MasterclassClosed),
            _ => new SessionAdvance(null, null),
        };
    }

    /// <summary>Where the Session stands and when its next step is due; null if there is no such Session.</summary>
    public async Task<SessionProgress?> GetSessionProgressAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SessionId", sessionId);
        parameters.Add("RevealPauseMs", Settings.RevealPauseMilliseconds);
        var row = await QuerySingleOrDefaultAsync<ProgressRow>("dbo.GetSessionProgress", parameters, cancellationToken);
        return row is null
            ? null
            : new SessionProgress(row.SessionId, row.MasterclassId, row.Step, row.DueAt is { } due ? Utc(due) : null);
    }

    private QuizSettings Settings => settings ?? new QuizSettings();

    private sealed class ProgressRow
    {
        public int SessionId { get; init; }
        public int MasterclassId { get; init; }
        public SessionStep Step { get; init; }
        public DateTime? DueAt { get; init; }
    }

    private sealed class AdvanceRow
    {
        public Advanced? Advanced { get; init; }
        public int? MasterclassId { get; init; }
        public DateTime? EndedAt { get; init; }
        public bool MasterclassClosed { get; init; }
    }

    // dbo.AdvanceSession's Advanced: what it did (NULL: nothing).
    private enum Advanced
    {
        Started,
        Ended,
    }

    private sealed class SessionQuestionRow
    {
        public int SessionId { get; init; }
        public int Number { get; init; }
        public int QuestionId { get; init; }
        public string StableId { get; init; } = "";
        public int Difficulty { get; init; }
    }
}
