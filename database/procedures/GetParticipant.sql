-- The Participant whose browser holds @Token (no row if the token belongs to nobody).
CREATE OR ALTER PROCEDURE dbo.GetParticipant
    @Token varchar(200)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.Id, p.MasterclassId, m.Name AS MasterclassName, CAST(m.Status AS int) AS MasterclassStatus,
           p.Nickname, p.JoinedAt
    FROM dbo.Participants p
    JOIN dbo.Masterclasses m ON m.Id = p.MasterclassId
    WHERE p.TokenHash = HASHBYTES('SHA2_256', @Token);
END
