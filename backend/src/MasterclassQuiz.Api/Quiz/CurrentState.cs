using System.Text.Json.Serialization;

namespace MasterclassQuiz.Api.Quiz;

/// <summary>
/// What a Masterclass's screens should show right now (the <c>GetCurrentState</c> reply), for a phone or
/// projector that (re)connects and missed the broadcasts. <see cref="Step"/> says which part is set:
/// <list type="bullet">
/// <item><see cref="LiveStep.Waiting"/>: no Session has started yet; nothing else is set.</item>
/// <item><see cref="LiveStep.Question"/>: <see cref="Question"/> runs (never with its correct option), with its <see cref="Count"/>.</item>
/// <item><see cref="LiveStep.Revealed"/>: <see cref="Revealed"/> is the latest Question, revealed; the reveal pause runs.</item>
/// <item><see cref="LiveStep.Ended"/>: the latest Session has <see cref="Ended"/>; the next one hasn't started.</item>
/// </list>
/// <see cref="Participant"/> is set only when asked for by a Participant of this Masterclass.
/// </summary>
public sealed record CurrentState(
    int MasterclassId,
    LiveStep Step,
    QuestionStarted? Question,
    AnswerCount? Count,
    QuestionRevealed? Revealed,
    SessionEnded? Ended,
    ParticipantState? Participant);

[JsonConverter(typeof(JsonStringEnumConverter<LiveStep>))]
public enum LiveStep
{
    Waiting,
    Question,
    Revealed,
    Ended,
}

/// <summary>
/// The asking Participant's own part of the <see cref="CurrentState"/>: their Answer to the Question shown
/// (null while Waiting or once the Session ended).
/// </summary>
public sealed record ParticipantState(int ParticipantId, string Nickname, OwnAnswer? Answer);

/// <summary>
/// A Participant's Answer to the Question shown. <see cref="CanAnswer"/> is false when they joined after it
/// started. <see cref="Option"/> is the option they chose (null: not answered, or Timed out).
/// <see cref="Outcome"/> is set only once the Question is revealed, so it never gives the correct option away.
/// </summary>
public sealed record OwnAnswer(int QuestionId, bool CanAnswer, int? Option, AnswerOutcome? Outcome);

[JsonConverter(typeof(JsonStringEnumConverter<AnswerOutcome>))]
public enum AnswerOutcome
{
    Correct = 1,
    Incorrect = 2,
    TimedOut = 3,
}
