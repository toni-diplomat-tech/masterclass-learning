-- The Session's most recently started Question as one JSON document, as Participants may see it: its
-- text, options (position and text, never which one is correct), start time and deadline (UTC, with Z).
-- NULL when no Question of the Session has started.
CREATE OR ALTER PROCEDURE dbo.GetStartedQuestion
    @SessionId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT (
        SELECT TOP (1)
            s.MasterclassId AS masterclassId,
            s.Id AS sessionId,
            w.Title AS workshopTitle,
            q.Id AS questionId,
            sq.Position AS number,
            (SELECT COUNT(*) FROM dbo.SessionQuestions a WHERE a.SessionId = s.Id) AS questionCount,
            q.Difficulty AS difficulty,
            q.Text AS text,
            JSON_QUERY(ISNULL((
                SELECT o.Position AS position, o.Text AS text
                FROM dbo.QuestionOptions o
                WHERE o.QuestionId = q.Id AND o.RetiredAt IS NULL
                ORDER BY o.Position
                FOR JSON PATH), N'[]')) AS options,
            CONVERT(nvarchar(30), sq.StartedAt, 127) + N'Z' AS startedAt,
            CONVERT(nvarchar(30), sq.Deadline, 127) + N'Z' AS deadline
        FROM dbo.SessionQuestions sq
        JOIN dbo.Sessions s ON s.Id = sq.SessionId
        JOIN dbo.Workshops w ON w.Id = s.WorkshopId
        JOIN dbo.Questions q ON q.Id = sq.QuestionId
        WHERE sq.SessionId = @SessionId AND sq.StartedAt IS NOT NULL
        ORDER BY sq.Position DESC
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS Question;
END
