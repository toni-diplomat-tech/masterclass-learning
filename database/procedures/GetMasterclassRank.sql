-- One Participant's rank on their Masterclass leaderboard as one JSON document: their rank and points,
-- ranked as dbo.GetMasterclassLeaderboard ranks (points summed over the Answers to revealed Questions in
-- all the Masterclass's Sessions; equal points share a rank), and how many Participants are on it. NULL
-- when there is no such Participant or they are not on the leaderboard (no Answer to a revealed Question).
CREATE OR ALTER PROCEDURE dbo.GetMasterclassRank
    @ParticipantId int
AS
BEGIN
    SET NOCOUNT ON;

    WITH Totals AS (
        SELECT a.ParticipantId, SUM(a.Points) AS Points
        FROM dbo.Answers a
        JOIN dbo.SessionQuestions sq ON sq.Id = a.SessionQuestionId
        JOIN dbo.Sessions s ON s.Id = sq.SessionId
        WHERE s.MasterclassId = (SELECT MasterclassId FROM dbo.Participants WHERE Id = @ParticipantId)
            AND sq.RevealedAt IS NOT NULL
        GROUP BY a.ParticipantId
    ),
    Ranked AS (
        SELECT ParticipantId, Points,
               RANK() OVER (ORDER BY Points DESC) AS [Rank],
               COUNT(*) OVER () AS [Of]
        FROM Totals
    )
    SELECT (
        SELECT r.[Rank] AS rank, r.Points AS points, r.[Of] AS [of]
        FROM Ranked r
        WHERE r.ParticipantId = @ParticipantId
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS MasterclassRank;
END
