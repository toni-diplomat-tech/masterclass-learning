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
/// The Quiz settings from configuration (section <see cref="SectionName"/>). Defaults are the spec's.
/// </summary>
public sealed class QuizSettings
{
    public const string SectionName = "Quiz";

    /// <summary>Time to answer a Question, in seconds, per Difficulty.</summary>
    public TimeLimitSeconds TimeLimitSeconds { get; init; } = new();
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
