namespace MasterclassQuiz.Api.Quiz;

/// <summary>
/// The Host's results of a Masterclass: its Sessions in Course order with per-Question results, and every
/// Participant by Nickname with their points and the token of their Recap link.
/// </summary>
public sealed record MasterclassResults(
    int MasterclassId,
    string MasterclassName,
    MasterclassStatus MasterclassStatus,
    string CourseTitle,
    IReadOnlyList<SessionResults> Sessions,
    IReadOnlyList<ParticipantResult> Participants);

/// <summary>
/// A Session's results: its revealed Questions in the order they ran (a running Question appears once
/// revealed). <see cref="Ended"/>: the Session is over, so these are final.
/// </summary>
public sealed record SessionResults(int SessionId, int WorkshopNumber, string WorkshopTitle, bool Ended, IReadOnlyList<QuestionResult> Questions);

/// <summary>
/// How a revealed Question went: Answers by outcome and the share of them that were Correct, rounded to a
/// whole percent (null when nobody was there to answer it).
/// </summary>
public sealed record QuestionResult(int Number, Difficulty Difficulty, string Text, int Correct, int Incorrect, int TimedOut, int? PercentCorrect);

/// <summary>A Participant's points over every revealed Question of the Masterclass, and their Recap token (<c>/recap/&lt;token&gt;</c>).</summary>
public sealed record ParticipantResult(int ParticipantId, string Nickname, int Points, string RecapToken);
