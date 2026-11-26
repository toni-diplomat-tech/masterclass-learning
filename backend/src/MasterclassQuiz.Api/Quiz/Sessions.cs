using System.Text.Json.Serialization;

namespace MasterclassQuiz.Api.Quiz;

/// <summary>
/// The started Session, or every reason it wasn't started (then <see cref="Session"/> is null).
/// </summary>
public sealed record SessionStartResult(StartedSession? Session, IReadOnlyList<string> Problems)
{
    public bool Succeeded => Problems.Count == 0;
}

/// <summary>
/// A Session that has just started: its selected Questions in run order (Easy to Hard) and its first
/// Question, which is already running. Only <see cref="FirstQuestion"/> may be shown to Participants.
/// </summary>
public sealed record StartedSession(int Id, int MasterclassId, int WorkshopId, IReadOnlyList<SessionQuestion> Questions, QuestionStarted FirstQuestion);

/// <summary>One selected Question of a Session; <see cref="Number"/> is its place in the run order, from 1.</summary>
public sealed record SessionQuestion(int Number, int QuestionId, string StableId, Difficulty Difficulty);

/// <summary>
/// A running Question as Participants and the projector see it (the <c>QuestionStarted</c> message):
/// never which option is correct. <see cref="StartedAt"/> and <see cref="Deadline"/> are the server's UTC
/// times; clients count down to the deadline for display only.
/// </summary>
public sealed record QuestionStarted(
    int MasterclassId,
    int SessionId,
    string WorkshopTitle,
    int QuestionId,
    int Number,
    int QuestionCount,
    Difficulty Difficulty,
    string Text,
    IReadOnlyList<QuestionOption> Options,
    DateTime StartedAt,
    DateTime Deadline);

/// <summary>An answer option as Participants see it, identified by its position (from 1).</summary>
public sealed record QuestionOption(int Position, string Text);

/// <summary>
/// A revealed Question (the <c>QuestionRevealed</c> message): the Question as it ran, its correct option,
/// how many Participants chose each option, how many answered and how many were Timed out.
/// <see cref="LastQuestion"/>: no Question of the Session follows, so the Session ends after the pause.
/// </summary>
public sealed record QuestionRevealed(
    int MasterclassId,
    int SessionId,
    string WorkshopTitle,
    int QuestionId,
    int Number,
    int QuestionCount,
    Difficulty Difficulty,
    string Text,
    int CorrectOption,
    IReadOnlyList<RevealedOption> Options,
    int Answered,
    int TimedOut,
    DateTime RevealedAt,
    bool LastQuestion);

/// <summary>An option of a revealed Question and how many Participants chose it.</summary>
public sealed record RevealedOption(int Position, string Text, int Answers);

/// <summary>A Session that has ended (the <c>SessionEnded</c> message), at the server's UTC time.</summary>
public sealed record SessionEnded(int MasterclassId, int SessionId, DateTime EndedAt);

/// <summary>
/// What <see cref="QuizEngine.AdvanceSessionAsync"/> did: started the next Question, ended the Session,
/// or nothing (both null) because a Question is running, the reveal pause lasts, or the Session ended earlier.
/// <see cref="MasterclassClosed"/> is true when ending the last Workshop's Session also made the
/// Masterclass Closed.
/// </summary>
public sealed record SessionAdvance(QuestionStarted? Started, SessionEnded? Ended, bool MasterclassClosed = false);

/// <summary>
/// Where a Session stands and when its next step is due (UTC; null once ended): at <see cref="DueAt"/>
/// a running Question is revealed (<see cref="QuizEngine.RevealQuestionAsync"/>), or a revealed one's
/// pause is over and the Session moves on (<see cref="QuizEngine.AdvanceSessionAsync"/>).
/// </summary>
public sealed record SessionProgress(int SessionId, int MasterclassId, SessionStep Step, DateTime? DueAt);

[JsonConverter(typeof(JsonStringEnumConverter<SessionStep>))]
public enum SessionStep
{
    /// <summary>A Question takes Answers; it is revealed at its deadline plus grace, or once everyone answered.</summary>
    QuestionRunning,
    /// <summary>The latest Question is revealed; the reveal pause runs.</summary>
    QuestionRevealed,
    Ended,
}

/// <summary>
/// The Quiz settings from configuration (section <see cref="SectionName"/>). Defaults are the spec's.
/// </summary>
public sealed class QuizSettings
{
    public const string SectionName = "Quiz";

    /// <summary>Time to answer a Question, in seconds, per Difficulty.</summary>
    public TimeLimitSeconds TimeLimitSeconds { get; init; } = new();

    /// <summary>How long a revealed Question stays up before the next one starts (or the Session ends).</summary>
    public double RevealPauseSeconds { get; init; } = 5;

    public int RevealPauseMilliseconds => (int)Math.Round(1000 * RevealPauseSeconds);
}

public sealed class TimeLimitSeconds
{
    public double Easy { get; init; } = 5;
    public double Medium { get; init; } = 7;
    public double Hard { get; init; } = 10;

    public int MillisecondsFor(Difficulty difficulty) => (int)Math.Round(1000 * difficulty switch
    {
        Difficulty.Easy => Easy,
        Difficulty.Medium => Medium,
        _ => Hard,
    });
}
