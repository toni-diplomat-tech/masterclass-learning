-- The Ids of the Sessions that haven't ended (EndedAt IS NULL), oldest first: those the backend timer must
-- follow, e.g. again after a restart. With @MasterclassId, only that Masterclass's (at most one).
CREATE OR ALTER PROCEDURE dbo.ListRunningSessions
    @MasterclassId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT s.Id
    FROM dbo.Sessions s
    WHERE s.EndedAt IS NULL AND (@MasterclassId IS NULL OR s.MasterclassId = @MasterclassId)
    ORDER BY s.Id;
END
