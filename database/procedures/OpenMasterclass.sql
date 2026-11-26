-- Opens a Draft Masterclass: it gets a 6-digit Join code that no other Open Masterclass has.
-- Opening an Open Masterclass again keeps its code; a Closed one stays Closed; an unknown id does nothing.
-- Takes time only from @Now and randomness only from @Seed (same seed, same codes drawn).
CREATE OR ALTER PROCEDURE dbo.OpenMasterclass
    @MasterclassId int,
    @Seed          int,
    @Now           datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- One opening at a time, so two Masterclasses can't both take the same free code.
    EXEC sp_getapplock @Resource = N'dbo.Masterclasses.JoinCode', @LockMode = N'Exclusive', @LockOwner = N'Transaction';

    IF NOT EXISTS (SELECT 1 FROM dbo.Masterclasses WITH (UPDLOCK) WHERE Id = @MasterclassId AND Status = 1)
    BEGIN
        COMMIT;
        RETURN;
    END

    -- Codes start at 100000, so they never lose a leading zero along the way.
    DECLARE @Code char(6), @Tries int = 0;
    DECLARE @Ignored float = RAND(@Seed);
    WHILE 1 = 1
    BEGIN
        SET @Code = CAST(100000 + FLOOR(RAND() * 900000) AS char(6));
        IF NOT EXISTS (SELECT 1 FROM dbo.Masterclasses WHERE Status = 2 AND JoinCode = @Code)
            BREAK;
        SET @Tries += 1;
        IF @Tries >= 10000
            THROW 50001, N'No free Join code was found.', 1;
    END

    UPDATE dbo.Masterclasses
    SET Status = 2, JoinCode = @Code, OpenedAt = @Now
    WHERE Id = @MasterclassId;

    COMMIT;
END
