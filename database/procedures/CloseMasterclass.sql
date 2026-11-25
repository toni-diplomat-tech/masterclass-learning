-- Closes an Open Masterclass when the meeting ends: no more joining or Sessions, and its Join code is
-- free for other Open Masterclasses (the unique index covers Open ones only; the Closed row keeps it).
-- Closing a Closed Masterclass again changes nothing. Takes time only from @Now.
--
-- Result set: the problems (one nvarchar per row); empty when the Masterclass is now Closed. An unknown
-- id gives no problem and does nothing (the caller finds no Masterclass).
CREATE OR ALTER PROCEDURE dbo.CloseMasterclass
    @MasterclassId int,
    @Now           datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Locking the Masterclass row makes closing take turns with joins and Session starts.
    DECLARE @Status tinyint = (
        SELECT Status FROM dbo.Masterclasses WITH (UPDLOCK, HOLDLOCK) WHERE Id = @MasterclassId);

    IF @Status = 1
    BEGIN
        COMMIT;
        SELECT N'Only an Open Masterclass can be closed.' AS Problem;
        RETURN;
    END

    IF @Status = 2 AND EXISTS (SELECT 1 FROM dbo.Sessions WHERE MasterclassId = @MasterclassId AND EndedAt IS NULL)
    BEGIN
        COMMIT;
        SELECT N'A Session is still running; close the Masterclass once it has ended.' AS Problem;
        RETURN;
    END

    UPDATE dbo.Masterclasses
    SET Status = 3, ClosedAt = @Now
    WHERE Id = @MasterclassId AND Status = 2;

    COMMIT;

    SELECT CAST(NULL AS nvarchar(max)) AS Problem WHERE 1 = 0; -- empty
END
