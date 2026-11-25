-- Where a Masterclass's live meeting stands, for a phone or projector that (re)connects. It looks at the
-- Masterclass's latest Session and that Session's latest started Question:
--   Step N'Waiting':  no Session has started;
--   Step N'Question': the Question runs; Answered/Participants count its Answers like dbo.SubmitAnswer;
--   Step N'Revealed': the Question is revealed and the Session hasn't ended (the reveal pause runs);
--   Step N'Ended':    the Session has ended (EndedAt).
-- With the @Token of a Participant of this Masterclass, also their Id and Nickname and, for a running or
-- revealed Question, whether they can answer it (they were in the Masterclass when it started), the option
-- they chose and, once revealed only, its Outcome (1 Correct, 2 Incorrect, 3 Timed out).
-- dbo.GetStartedQuestion / dbo.GetRevealedQuestion read the Question itself (QuestionId says which).
-- No row for an unknown Masterclass.
CREATE OR ALTER PROCEDURE dbo.GetCurrentState
    @MasterclassId int,
    @Token         varchar(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT m.Id AS MasterclassId,
           session.Id AS SessionId,
           CASE
               WHEN session.Id IS NULL THEN N'Waiting'
               WHEN session.EndedAt IS NOT NULL THEN N'Ended'
               WHEN question.RevealedAt IS NULL THEN N'Question'
               ELSE N'Revealed'
           END AS Step,
           session.EndedAt,
           question.QuestionId,
           (SELECT COUNT(*) FROM dbo.Answers a WHERE a.SessionQuestionId = question.Id AND a.Outcome <> 3) AS Answered,
           (SELECT COUNT(*) FROM dbo.Participants p WHERE p.MasterclassId = m.Id AND p.JoinedAt <= question.StartedAt) AS Participants,
           me.Id AS ParticipantId,
           me.Nickname,
           CAST(CASE WHEN me.JoinedAt <= question.StartedAt THEN 1 ELSE 0 END AS bit) AS CanAnswer,
           mine.OptionPosition,
           CASE WHEN question.RevealedAt IS NOT NULL THEN mine.Outcome END AS Outcome
    FROM dbo.Masterclasses m
    OUTER APPLY (
        SELECT TOP (1) s.Id, s.EndedAt FROM dbo.Sessions s
        WHERE s.MasterclassId = m.Id
        ORDER BY s.StartedAt DESC, s.Id DESC) session
    OUTER APPLY (
        SELECT TOP (1) sq.Id, sq.QuestionId, sq.StartedAt, sq.RevealedAt FROM dbo.SessionQuestions sq
        WHERE sq.SessionId = session.Id AND sq.StartedAt IS NOT NULL
        ORDER BY sq.Position DESC) question
    OUTER APPLY (
        SELECT p.Id, p.Nickname, p.JoinedAt FROM dbo.Participants p
        WHERE p.MasterclassId = m.Id AND p.TokenHash = HASHBYTES('SHA2_256', @Token)) me
    OUTER APPLY (
        SELECT a.OptionPosition, a.Outcome FROM dbo.Answers a
        WHERE a.SessionQuestionId = question.Id AND a.ParticipantId = me.Id) mine
    WHERE m.Id = @MasterclassId;
END
