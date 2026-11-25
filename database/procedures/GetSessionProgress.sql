-- Where a Session stands and when its next step is due, for the backend timer that moves it on:
--   Step N'QuestionRunning': a Question is running; DueAt = its AnswersCloseAt (dbo.RevealQuestion may
--                            reveal it earlier, once everyone has answered);
--   Step N'QuestionRevealed': the last started Question is revealed; DueAt = its reveal plus
--                            @RevealPauseMs, when dbo.AdvanceSession moves on;
--   Step N'Ended': DueAt is NULL.
-- No row for an unknown Session.
CREATE OR ALTER PROCEDURE dbo.GetSessionProgress
    @SessionId     int,
    @RevealPauseMs int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT s.Id AS SessionId, s.MasterclassId,
           CASE
               WHEN s.EndedAt IS NOT NULL THEN N'Ended'
               WHEN running.AnswersCloseAt IS NOT NULL THEN N'QuestionRunning'
               ELSE N'QuestionRevealed'
           END AS Step,
           CASE
               WHEN s.EndedAt IS NOT NULL THEN NULL
               WHEN running.AnswersCloseAt IS NOT NULL THEN running.AnswersCloseAt
               ELSE DATEADD(millisecond, @RevealPauseMs, revealed.RevealedAt)
           END AS DueAt
    FROM dbo.Sessions s
    OUTER APPLY (
        SELECT TOP (1) sq.AnswersCloseAt FROM dbo.SessionQuestions sq
        WHERE sq.SessionId = s.Id AND sq.StartedAt IS NOT NULL AND sq.RevealedAt IS NULL) running
    OUTER APPLY (
        SELECT MAX(sq.RevealedAt) AS RevealedAt FROM dbo.SessionQuestions sq
        WHERE sq.SessionId = s.Id) revealed
    WHERE s.Id = @SessionId;
END
