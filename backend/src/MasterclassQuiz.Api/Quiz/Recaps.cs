using System.Text.Json.Serialization;

namespace MasterclassQuiz.Api.Quiz;

/// <summary>
/// A Participant's Recap of a Masterclass: one entry per Workshop whose Session has ended, in Course order.
/// Workshops with no Session, or whose Session is still running, are left out.
/// </summary>
public sealed record Recap(
    string Nickname,
    int MasterclassId,
    string MasterclassName,
    MasterclassStatus MasterclassStatus,
    string CourseTitle,
    IReadOnlyList<RecapWorkshop> Workshops);

/// <summary>
/// How a Workshop's Session went for the Participant. <see cref="Lessons"/> is empty for a Perfect score,
/// holds the Lessons of the missed Questions for <see cref="RecapOutcome.MissedQuestions"/>, and every
/// Lesson of the Workshop (without Questions) when Not attended. Lessons are in Course order.
/// </summary>
public sealed record RecapWorkshop(int Number, string Title, RecapOutcome Outcome, IReadOnlyList<RecapLesson> Lessons);

/// <summary>Perfect score: every Answer Correct. Not attended: no Answer at all in the Session (joined after it).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecapOutcome>))]
public enum RecapOutcome
{
    PerfectScore,
    MissedQuestions,
    NotAttended,
}

/// <summary>A Lesson to revisit, at its stored <see cref="Url"/>, with the Questions on it the Participant missed (in Session order).</summary>
public sealed record RecapLesson(int Number, string Title, string Url, IReadOnlyList<MissedQuestion> Questions);

/// <summary>A Question answered Incorrect (<see cref="Answer"/> is the chosen option's text) or Timed out (<see cref="Answer"/> is null).</summary>
public sealed record MissedQuestion(string Text, string? Answer, bool TimedOut, string CorrectOption);
