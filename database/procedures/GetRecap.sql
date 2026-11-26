-- A Participant's Recap of their Masterclass as one JSON document; NULL if @RecapToken belongs to nobody.
-- One entry per Workshop whose Session has ended (EndedAt set), in Course order; Workshops without a
-- Session, or whose Session is still running, are left out so nobody can look answers up mid-quiz.
-- outcome per Workshop, from the Participant's Answers in that Session:
--   NotAttended     no Answer at all (they joined after it): lessons = every current Lesson of the Workshop;
--   PerfectScore    every Answer Correct: lessons = [];
--   MissedQuestions lessons = the Lessons of their Incorrect and Timed out Answers, each with those
--                   Questions in Session order: the Question, their option's text (null when Timed out)
--                   and the correct option's text, both from the options the Question ran with
--                   (dbo.SessionQuestionOptions), so a later re-import doesn't change them.
-- A Question that started before the Participant joined has no Answer, so it is neither missed nor Correct.
-- No time rule here, so no @Now: the Recap reads the same whenever it is opened, Masterclass closed or not.
CREATE OR ALTER PROCEDURE dbo.GetRecap
    @RecapToken varchar(200)
AS
BEGIN
    SET NOCOUNT ON;

    -- The Participant's Answers in the Masterclass's ended Sessions.
    DECLARE @Answers TABLE (
        SessionId int NOT NULL, SessionQuestionId int NOT NULL, Position int NOT NULL, QuestionId int NOT NULL,
        LessonId int NOT NULL, Outcome tinyint NOT NULL, OptionPosition tinyint NULL);

    INSERT @Answers (SessionId, SessionQuestionId, Position, QuestionId, LessonId, Outcome, OptionPosition)
    SELECT s.Id, sq.Id, sq.Position, sq.QuestionId, q.LessonId, a.Outcome, a.OptionPosition
    FROM dbo.Participants p
    JOIN dbo.Sessions s ON s.MasterclassId = p.MasterclassId AND s.EndedAt IS NOT NULL
    JOIN dbo.SessionQuestions sq ON sq.SessionId = s.Id
    JOIN dbo.Questions q ON q.Id = sq.QuestionId
    JOIN dbo.Answers a ON a.SessionQuestionId = sq.Id AND a.ParticipantId = p.Id
    WHERE p.RecapToken = @RecapToken;

    SELECT (
        SELECT
            p.Nickname AS nickname,
            m.Id AS masterclassId,
            m.Name AS masterclassName,
            CAST(m.Status AS int) AS masterclassStatus,
            c.Title AS courseTitle,
            JSON_QUERY(ISNULL((
                SELECT
                    w.Number AS number,
                    w.Title AS title,
                    x.Outcome AS outcome,
                    JSON_QUERY(CASE x.Outcome
                        WHEN N'NotAttended' THEN ISNULL((
                            SELECT l.Number AS number, l.Title AS title, l.Url AS url, JSON_QUERY(N'[]') AS questions
                            FROM dbo.Lessons l
                            WHERE l.WorkshopId = w.Id AND l.RetiredAt IS NULL
                            ORDER BY l.Number
                            FOR JSON PATH), N'[]')
                        WHEN N'MissedQuestions' THEN (
                            SELECT l.Number AS number, l.Title AS title, l.Url AS url,
                                   JSON_QUERY((
                                       SELECT q.Text AS text,
                                              chosen.Text AS answer,
                                              CAST(CASE WHEN a.Outcome = 3 THEN 1 ELSE 0 END AS bit) AS timedOut,
                                              (SELECT TOP (1) o.Text FROM dbo.SessionQuestionOptions o
                                               WHERE o.SessionQuestionId = a.SessionQuestionId AND o.IsCorrect = 1) AS correctOption
                                       FROM @Answers a
                                       JOIN dbo.Questions q ON q.Id = a.QuestionId
                                       LEFT JOIN dbo.SessionQuestionOptions chosen
                                           ON chosen.SessionQuestionId = a.SessionQuestionId AND chosen.Position = a.OptionPosition
                                       WHERE a.SessionId = s.Id AND a.Outcome <> 1 AND a.LessonId = l.Id
                                       ORDER BY a.Position
                                       FOR JSON PATH, INCLUDE_NULL_VALUES)) AS questions
                            FROM dbo.Lessons l
                            WHERE EXISTS (SELECT 1 FROM @Answers a WHERE a.SessionId = s.Id AND a.Outcome <> 1 AND a.LessonId = l.Id)
                            ORDER BY l.Number
                            FOR JSON PATH)
                        ELSE N'[]' END) AS lessons
                FROM dbo.Sessions s
                JOIN dbo.Workshops w ON w.Id = s.WorkshopId
                CROSS APPLY (
                    SELECT CASE
                        WHEN NOT EXISTS (SELECT 1 FROM @Answers a WHERE a.SessionId = s.Id) THEN N'NotAttended'
                        WHEN NOT EXISTS (SELECT 1 FROM @Answers a WHERE a.SessionId = s.Id AND a.Outcome <> 1) THEN N'PerfectScore'
                        ELSE N'MissedQuestions' END AS Outcome
                ) x
                WHERE s.MasterclassId = m.Id AND s.EndedAt IS NOT NULL
                ORDER BY w.Number
                FOR JSON PATH), N'[]')) AS workshops
        FROM dbo.Participants p
        JOIN dbo.Masterclasses m ON m.Id = p.MasterclassId
        JOIN dbo.Courses c ON c.Id = m.CourseId
        WHERE p.RecapToken = @RecapToken
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS Recap;
END
