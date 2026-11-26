-- The Session's most recently revealed Question as one JSON document: the Question as it ran, the
-- correct option and options it ran with (dbo.SessionQuestionOptions), how many Participants chose each option, how many answered and how many were Timed
-- out, and when it was revealed (UTC, with Z). lastQuestion is true for the Session's last Question.
-- NULL when no Question of the Session has been revealed.
CREATE OR ALTER PROCEDURE dbo.GetRevealedQuestion
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
            (SELECT TOP (1) o.Position FROM dbo.SessionQuestionOptions o
             WHERE o.SessionQuestionId = sq.Id AND o.IsCorrect = 1) AS correctOption,
            JSON_QUERY(ISNULL((
                SELECT o.Position AS position, o.Text AS text,
                       (SELECT COUNT(*) FROM dbo.Answers a
                        WHERE a.SessionQuestionId = sq.Id AND a.OptionPosition = o.Position) AS answers
                FROM dbo.SessionQuestionOptions o
                WHERE o.SessionQuestionId = sq.Id
                ORDER BY o.Position
                FOR JSON PATH), N'[]')) AS options,
            (SELECT COUNT(*) FROM dbo.Answers a WHERE a.SessionQuestionId = sq.Id AND a.Outcome <> 3) AS answered,
            (SELECT COUNT(*) FROM dbo.Answers a WHERE a.SessionQuestionId = sq.Id AND a.Outcome = 3) AS timedOut,
            CONVERT(nvarchar(30), sq.RevealedAt, 127) + N'Z' AS revealedAt,
            CAST(CASE WHEN NOT EXISTS (
                SELECT 1 FROM dbo.SessionQuestions later
                WHERE later.SessionId = s.Id AND later.Position > sq.Position) THEN 1 ELSE 0 END AS bit) AS lastQuestion
        FROM dbo.SessionQuestions sq
        JOIN dbo.Sessions s ON s.Id = sq.SessionId
        JOIN dbo.Workshops w ON w.Id = s.WorkshopId
        JOIN dbo.Questions q ON q.Id = sq.QuestionId
        WHERE sq.SessionId = @SessionId AND sq.RevealedAt IS NOT NULL
        ORDER BY sq.Position DESC
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS Question;
END
