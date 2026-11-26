-- The Recap token of the Participant whose browser holds @Token (no row if the token belongs to nobody).
CREATE OR ALTER PROCEDURE dbo.GetRecapToken
    @Token varchar(200)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.RecapToken
    FROM dbo.Participants p
    WHERE p.TokenHash = HASHBYTES('SHA2_256', @Token);
END
