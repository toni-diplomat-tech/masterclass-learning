-- Moves a running Session on once the reveal pause (@RevealPauseMs, from configuration) after its last
-- revealed Question has passed: starts the next Question (dbo.StartNextQuestion), or, after the last
-- Question, ends the Session (EndedAt = @Now). Ending the last Workshop's Session (no current Workshop
-- of the Course is left without its Session) also makes the Open Masterclass Closed (ClosedAt = @Now),
-- in the same transaction. Does nothing while a Question is running or the pause lasts, or once the
-- Session has ended. Takes time only from @Now.
--
-- Returns one row: Advanced = N'Started' (dbo.GetStartedQuestion reads the new Question), N'Ended',
-- or NULL when it did nothing; the Session's MasterclassId; its EndedAt (set once it has ended); and
-- MasterclassClosed, 1 when this call closed the Masterclass.
CREATE OR ALTER PROCEDURE dbo.AdvanceSession
    @SessionId     int,
    @RevealPauseMs int,
    @Now           datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Locking the Masterclass row first, as closing and Session starts do, keeps the lock order the same;
    -- locking the Session row then makes its advances take turns.
    DECLARE @MasterclassId int = (SELECT MasterclassId FROM dbo.Sessions WHERE Id = @SessionId);
    DECLARE @Locked int, @EndedAt datetime2(7);
    SELECT @Locked = Id FROM dbo.Masterclasses WITH (UPDLOCK, HOLDLOCK) WHERE Id = @MasterclassId;
    SELECT @EndedAt = EndedAt FROM dbo.Sessions WITH (UPDLOCK, HOLDLOCK) WHERE Id = @SessionId;

    IF @MasterclassId IS NULL OR @EndedAt IS NOT NULL
        OR EXISTS (
            SELECT 1 FROM dbo.SessionQuestions
            WHERE SessionId = @SessionId AND StartedAt IS NOT NULL AND RevealedAt IS NULL)
        OR @Now < (
            SELECT DATEADD(millisecond, @RevealPauseMs, MAX(RevealedAt)) FROM dbo.SessionQuestions
            WHERE SessionId = @SessionId)
    BEGIN
        COMMIT;
        SELECT CAST(NULL AS nvarchar(10)) AS Advanced, @MasterclassId AS MasterclassId, @EndedAt AS EndedAt,
            CAST(0 AS bit) AS MasterclassClosed;
        RETURN;
    END

    DECLARE @Advanced nvarchar(10), @MasterclassClosed bit = 0;
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

        -- The last Workshop's Session has ended: the Masterclass ends with it.
        UPDATE m
        SET Status = 3, ClosedAt = @Now
        FROM dbo.Masterclasses m
        WHERE m.Id = @MasterclassId AND m.Status = 2
            AND NOT EXISTS (
                SELECT 1 FROM dbo.Workshops w
                WHERE w.CourseId = m.CourseId AND w.RetiredAt IS NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM dbo.Sessions s WHERE s.MasterclassId = m.Id AND s.WorkshopId = w.Id));
        IF @@ROWCOUNT = 1
            SET @MasterclassClosed = 1;
    END

    COMMIT;
    SELECT @Advanced AS Advanced, @MasterclassId AS MasterclassId, @EndedAt AS EndedAt,
        @MasterclassClosed AS MasterclassClosed;
END
