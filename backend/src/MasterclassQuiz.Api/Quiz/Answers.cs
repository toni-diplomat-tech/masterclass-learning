using System.Text.Json.Serialization;

namespace MasterclassQuiz.Api.Quiz;

/// <summary>
/// What the server made of a submitted Answer. Only <see cref="Accepted"/> records it; the others say
/// why not. Accepted never tells whether the Answer is correct: that waits for the reveal.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnswerResult>))]
public enum AnswerResult
{
    Accepted,
    /// <summary>Past the deadline plus the 500 ms grace, or the Question was already revealed.</summary>
    TooLate,
    /// <summary>The Participant already answered this Question; an Answer can't be changed.</summary>
    AlreadyAnswered,
    /// <summary>The Participant joined after the Question started, so it isn't theirs to answer.</summary>
    JoinedAfterStart,
    /// <summary>The Question has no such option.</summary>
    UnknownOption,
    /// <summary>The Question hasn't run in the Participant's Masterclass.</summary>
    NoSuchQuestion,
    /// <summary>The token belongs to nobody.</summary>
    UnknownParticipant,
}

/// <summary>
/// The result of <see cref="QuizEngine.SubmitAnswerAsync"/>, with the Question's answer count when the
/// Question was found (null otherwise).
/// </summary>
public sealed record AnswerSubmission(AnswerResult Result, AnswerCount? Count);

/// <summary>
/// How many of the Participants who can answer a running Question (those in the Masterclass when it
/// started) have answered it: the <c>AnswerCountUpdated</c> message.
/// </summary>
public sealed record AnswerCount(int MasterclassId, int SessionId, int QuestionId, int Answered, int Participants)
{
    /// <summary>Every Participant who can answer has answered (and there is at least one).</summary>
    [JsonIgnore]
    public bool EveryoneAnswered => Participants > 0 && Answered >= Participants;
}
