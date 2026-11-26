-- Opens a Draft Masterclass: it gets a 6-digit Join code. Only one Masterclass can be Open at a time, so
-- a Draft can't open while another Masterclass is Open; the Host closes that one first.
-- Opening the Open Masterclass again keeps its code; a Closed one stays Closed.
-- Takes time only from @Now and randomness only from @Seed (same seed, same code drawn).
--
-- Result set: the problems (one nvarchar per row); empty when the Masterclass is now Open. An unknown
-- id gives no problem and does nothing (the caller finds no Masterclass).
CREATE OR ALTER PROCEDURE dbo.OpenMasterclass
    @MasterclassId int,
    @Seed          int,
    @Now           datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- One opening at a time, so two Drafts can't both see that nothing is Open and both open (the
    -- unique index UQ_Masterclasses_OneOpen backs this up: a second Open row fails instead of slipping in).
    DECLARE @Lock int;
    EXEC @Lock = sp_getapplock @Resource = N'dbo.Masterclasses.Open', @LockMode = N'Exclusive', @LockOwner = N'Transaction';
    IF @Lock < 0
        THROW 50001, N'The lock for opening a Masterclass was not granted, so nothing was opened.', 1;

    DECLARE @Status tinyint = (
        SELECT Status FROM dbo.Masterclasses WITH (UPDLOCK, HOLDLOCK) WHERE Id = @MasterclassId);

    IF @Status = 3
    BEGIN
        COMMIT;
        SELECT N'A Closed Masterclass can''t be opened again.' AS Problem;
        RETURN;
    END

    DECLARE @OpenName nvarchar(200) = (
        SELECT TOP (1) Name FROM dbo.Masterclasses WHERE Status = 2 AND Id <> @MasterclassId ORDER BY OpenedAt);

    IF @Status = 1 AND @OpenName IS NOT NULL
    BEGIN
        COMMIT;
        SELECT N'Close "' + @OpenName + N'" first.' AS Problem;
        RETURN;
    END

    IF @Status = 1
    BEGIN
        -- Codes start at 100000, so they never lose a leading zero along the way.
        DECLARE @Ignored float = RAND(@Seed);
        UPDATE dbo.Masterclasses
        SET Status = 2, JoinCode = CAST(100000 + FLOOR(RAND() * 900000) AS char(6)), OpenedAt = @Now
        WHERE Id = @MasterclassId;
    END

    COMMIT;

    SELECT CAST(NULL AS nvarchar(max)) AS Problem WHERE 1 = 0; -- empty
END
