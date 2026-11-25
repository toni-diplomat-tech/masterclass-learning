-- A Session's leaderboard as one JSON document: every Participant with an Answer to one of its revealed
-- Questions, by points (the sum of those Answers' points), highest first. Participants with the same
-- points share a rank (1, 1, 3). questionPoints is what each scored on the latest revealed Question.
-- Answers to a Question that is still running don't count yet, so nothing leaks before its reveal.
-- ended is true once the Session has ended. NULL when there is no such Session.
CREATE OR ALTER PROCEDURE dbo.GetSessionLeaderboard
    @SessionId int
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LatestRevealed int = (
        SELECT TOP (1) Id FROM dbo.SessionQuestions
        WHERE SessionId = @SessionId AND RevealedAt IS NOT NULL
        ORDER BY Position DESC);

    SELECT (
        SELECT
            s.MasterclassId AS masterclassId,
            s.Id AS sessionId,
            w.Title AS workshopTitle,
            CAST(CASE WHEN s.EndedAt IS NULL THEN 0 ELSE 1 END AS bit) AS ended,
            JSON_QUERY(ISNULL((
                SELECT RANK() OVER (ORDER BY t.Points DESC) AS rank,
                       t.ParticipantId AS participantId,
                       p.Nickname AS nickname,
                       t.Points AS points,
                       t.QuestionPoints AS questionPoints
                FROM (
                    SELECT a.ParticipantId,
                           SUM(a.Points) AS Points,
                           SUM(CASE WHEN sq.Id = @LatestRevealed THEN a.Points ELSE 0 END) AS QuestionPoints
                    FROM dbo.Answers a
                    JOIN dbo.SessionQuestions sq ON sq.Id = a.SessionQuestionId
                    WHERE sq.SessionId = s.Id AND sq.RevealedAt IS NOT NULL
                    GROUP BY a.ParticipantId
                ) t
                JOIN dbo.Participants p ON p.Id = t.ParticipantId
                ORDER BY t.Points DESC, p.Nickname
                FOR JSON PATH), N'[]')) AS entries
        FROM dbo.Sessions s
        JOIN dbo.Workshops w ON w.Id = s.WorkshopId
        WHERE s.Id = @SessionId
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS Leaderboard;
END
