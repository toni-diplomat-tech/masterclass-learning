-- The Masterclass leaderboard as one JSON document: every Participant with an Answer to a revealed
-- Question in any of its Sessions, by points (the sum of those Answers' points over all Sessions),
-- highest first. Participants with the same points share a rank (1, 1, 3). Answers to a Question that
-- is still running don't count yet. NULL when there is no such Masterclass.
CREATE OR ALTER PROCEDURE dbo.GetMasterclassLeaderboard
    @MasterclassId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT (
        SELECT
            m.Id AS masterclassId,
            JSON_QUERY(ISNULL((
                SELECT RANK() OVER (ORDER BY t.Points DESC) AS rank,
                       t.ParticipantId AS participantId,
                       p.Nickname AS nickname,
                       t.Points AS points
                FROM (
                    SELECT a.ParticipantId, SUM(a.Points) AS Points
                    FROM dbo.Answers a
                    JOIN dbo.SessionQuestions sq ON sq.Id = a.SessionQuestionId
                    JOIN dbo.Sessions s ON s.Id = sq.SessionId
                    WHERE s.MasterclassId = m.Id AND sq.RevealedAt IS NOT NULL
                    GROUP BY a.ParticipantId
                ) t
                JOIN dbo.Participants p ON p.Id = t.ParticipantId
                ORDER BY t.Points DESC, p.Nickname
                FOR JSON PATH), N'[]')) AS entries
        FROM dbo.Masterclasses m
        WHERE m.Id = @MasterclassId
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS Leaderboard;
END
