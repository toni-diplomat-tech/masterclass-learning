-- The Host's results of a Masterclass as one JSON document; NULL if there is no such Masterclass.
-- sessions: every Session in Course order (Workshop number), each with its revealed Questions in the order
--   they ran: Correct, Incorrect and Timed out counts and percentCorrect = Correct / all three, rounded to
--   a whole percent (null when nobody was there to answer). A running Question appears once revealed, so
--   its counts are never partial.
-- participants: every Participant of the Masterclass by Nickname (also those with no Answer), with their
--   points over all revealed Questions and their Recap token, so the Host can open any Recap.
-- No status filter: the results read the same for Open and Closed Masterclasses.
CREATE OR ALTER PROCEDURE dbo.GetMasterclassResults
    @MasterclassId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT (
        SELECT
            m.Id AS masterclassId,
            m.Name AS masterclassName,
            CAST(m.Status AS int) AS masterclassStatus,
            c.Title AS courseTitle,
            JSON_QUERY(ISNULL((
                SELECT
                    s.Id AS sessionId,
                    w.Number AS workshopNumber,
                    w.Title AS workshopTitle,
                    CAST(CASE WHEN s.EndedAt IS NULL THEN 0 ELSE 1 END AS bit) AS ended,
                    JSON_QUERY(ISNULL((
                        SELECT
                            sq.Position AS number,
                            q.Difficulty AS difficulty,
                            q.Text AS text,
                            n.Correct AS correct,
                            n.Incorrect AS incorrect,
                            n.TimedOut AS timedOut,
                            CAST(ROUND(100.0 * n.Correct / NULLIF(n.Correct + n.Incorrect + n.TimedOut, 0), 0) AS int) AS percentCorrect
                        FROM dbo.SessionQuestions sq
                        JOIN dbo.Questions q ON q.Id = sq.QuestionId
                        CROSS APPLY (
                            SELECT ISNULL(SUM(CASE WHEN a.Outcome = 1 THEN 1 ELSE 0 END), 0) AS Correct,
                                   ISNULL(SUM(CASE WHEN a.Outcome = 2 THEN 1 ELSE 0 END), 0) AS Incorrect,
                                   ISNULL(SUM(CASE WHEN a.Outcome = 3 THEN 1 ELSE 0 END), 0) AS TimedOut
                            FROM dbo.Answers a
                            WHERE a.SessionQuestionId = sq.Id
                        ) n
                        WHERE sq.SessionId = s.Id AND sq.RevealedAt IS NOT NULL
                        ORDER BY sq.Position
                        FOR JSON PATH, INCLUDE_NULL_VALUES), N'[]')) AS questions
                FROM dbo.Sessions s
                JOIN dbo.Workshops w ON w.Id = s.WorkshopId
                WHERE s.MasterclassId = m.Id
                ORDER BY w.Number
                FOR JSON PATH), N'[]')) AS sessions,
            JSON_QUERY(ISNULL((
                SELECT
                    p.Id AS participantId,
                    p.Nickname AS nickname,
                    (SELECT ISNULL(SUM(a.Points), 0)
                     FROM dbo.Answers a
                     JOIN dbo.SessionQuestions sq ON sq.Id = a.SessionQuestionId
                     WHERE a.ParticipantId = p.Id AND sq.RevealedAt IS NOT NULL) AS points,
                    p.RecapToken AS recapToken
                FROM dbo.Participants p
                WHERE p.MasterclassId = m.Id
                ORDER BY p.Nickname
                FOR JSON PATH), N'[]')) AS participants
        FROM dbo.Masterclasses m
        JOIN dbo.Courses c ON c.Id = m.CourseId
        WHERE m.Id = @MasterclassId
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS Results;
END
