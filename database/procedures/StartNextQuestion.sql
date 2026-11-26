-- Starts a running Session's next Question (the lowest Position not started yet): records @Now as its
-- start and its deadline as @Now plus its time limit. Does nothing while a started Question is not yet
-- revealed, when every Question has started, or when the Session has ended. Takes time only from @Now.
-- It also records the Question's current options in dbo.SessionQuestionOptions: the options it runs with.
-- Returns nothing; dbo.GetStartedQuestion reads the Question that is now running.
CREATE OR ALTER PROCEDURE dbo.StartNextQuestion
    @SessionId int,
    @Now       datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Sessions WITH (UPDLOCK, HOLDLOCK) WHERE Id = @SessionId AND EndedAt IS NULL)
        OR EXISTS (
            SELECT 1 FROM dbo.SessionQuestions
            WHERE SessionId = @SessionId AND StartedAt IS NOT NULL AND RevealedAt IS NULL)
    BEGIN
        COMMIT;
        RETURN;
    END

    DECLARE @SessionQuestionId int = (
        SELECT TOP (1) Id FROM dbo.SessionQuestions
        WHERE SessionId = @SessionId AND StartedAt IS NULL
        ORDER BY Position);

    UPDATE dbo.SessionQuestions
    SET StartedAt = @Now, Deadline = DATEADD(millisecond, TimeLimitMs, @Now)
    WHERE Id = @SessionQuestionId;

    INSERT dbo.SessionQuestionOptions (SessionQuestionId, Position, Text, IsCorrect)
    SELECT sq.Id, o.Position, o.Text, o.IsCorrect
    FROM dbo.SessionQuestions sq
    JOIN dbo.QuestionOptions o ON o.QuestionId = sq.QuestionId AND o.RetiredAt IS NULL
    WHERE sq.Id = @SessionQuestionId;

    COMMIT;
END
