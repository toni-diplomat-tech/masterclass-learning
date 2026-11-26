-- Moves a running Session on once the reveal pause (@RevealPauseMs, from configuration) after its last
-- revealed Question has passed: starts the next Question (dbo.StartNextQuestion), or, after the last
-- Question, ends the Session (EndedAt = @Now). Does nothing while a Question is running or the pause
-- lasts, or once the Session has ended. Takes time only from @Now.
--
-- Returns one row: Advanced = N'Started' (dbo.GetStartedQuestion reads the new Question), N'Ended',
-- or NULL when it did nothing; the Session's MasterclassId; and its EndedAt (set once it has ended).
CREATE OR ALTER PROCEDURE dbo.AdvanceSession
    @SessionId     int,
    @RevealPauseMs int,
    @Now           datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Locking the Session row makes its advances take turns.
    DECLARE @MasterclassId int, @EndedAt datetime2(7);
    SELECT @MasterclassId = MasterclassId, @EndedAt = EndedAt
    FROM dbo.Sessions WITH (UPDLOCK, HOLDLOCK)
    WHERE Id = @SessionId;

    IF @MasterclassId IS NULL OR @EndedAt IS NOT NULL
        OR EXISTS (
            SELECT 1 FROM dbo.SessionQuestions
            WHERE SessionId = @SessionId AND StartedAt IS NOT NULL AND RevealedAt IS NULL)
        OR @Now < (
            SELECT DATEADD(millisecond, @RevealPauseMs, MAX(RevealedAt)) FROM dbo.SessionQuestions
            WHERE SessionId = @SessionId)
    BEGIN
        COMMIT;
        SELECT CAST(NULL AS nvarchar(10)) AS Advanced, @MasterclassId AS MasterclassId, @EndedAt AS EndedAt;
        RETURN;
    END

    DECLARE @Advanced nvarchar(10);
    IF EXISTS (SELECT 1 FROM dbo.SessionQuestions WHERE SessionId = @SessionId AND StartedAt IS NULL)
    BEGIN
        EXEC dbo.StartNextQuestion @SessionId = @SessionId, @Now = @Now;
        SET @Advanced = N'Started';
    END
    ELSE
    BEGIN
        UPDATE dbo.Sessions SET EndedAt = @Now WHERE Id = @SessionId;
        SET @EndedAt = @Now;
        SET @Advanced = N'Ended';
    END

    COMMIT;
    SELECT @Advanced AS Advanced, @MasterclassId AS MasterclassId, @EndedAt AS EndedAt;
END
