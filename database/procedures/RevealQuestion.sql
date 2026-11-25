-- Reveals a Session's running Question once it is due: when @Now has reached its AnswersCloseAt
-- (deadline + grace), or earlier once every Participant who was in the Masterclass when it started has
-- answered (and there is at least one). Records Timed out for each such Participant without an Answer;
-- Participants who joined after it started get no Answer for it. Takes time only from @Now.
--
-- Returns one row: Revealed (bit), 1 if this call revealed the Question. dbo.GetRevealedQuestion then
-- reads what it revealed.
CREATE OR ALTER PROCEDURE dbo.RevealQuestion
    @SessionId int,
    @Now       datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Locking the Session Question makes its reveal and its Answers take turns.
    DECLARE @SessionQuestionId int, @StartedAt datetime2(7), @AnswersCloseAt datetime2(7), @MasterclassId int;
    SELECT @SessionQuestionId = sq.Id, @StartedAt = sq.StartedAt, @AnswersCloseAt = sq.AnswersCloseAt,
           @MasterclassId = s.MasterclassId
    FROM dbo.SessionQuestions sq WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.Sessions s ON s.Id = sq.SessionId
    WHERE sq.SessionId = @SessionId AND sq.StartedAt IS NOT NULL AND sq.RevealedAt IS NULL;

    IF @SessionQuestionId IS NULL
    BEGIN
        COMMIT;
        SELECT CAST(0 AS bit) AS Revealed;
        RETURN;
    END

    DECLARE @Present TABLE (ParticipantId int PRIMARY KEY);
    INSERT @Present (ParticipantId)
    SELECT Id FROM dbo.Participants WHERE MasterclassId = @MasterclassId AND JoinedAt <= @StartedAt;

    DECLARE @Waiting int = (
        SELECT COUNT(*) FROM @Present p
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Answers a WHERE a.SessionQuestionId = @SessionQuestionId AND a.ParticipantId = p.ParticipantId));

    IF @Now < @AnswersCloseAt AND (@Waiting > 0 OR NOT EXISTS (SELECT 1 FROM @Present))
    BEGIN
        COMMIT;
        SELECT CAST(0 AS bit) AS Revealed;
        RETURN;
    END

    INSERT dbo.Answers (SessionQuestionId, ParticipantId, Outcome)
    SELECT @SessionQuestionId, p.ParticipantId, 3
    FROM @Present p
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Answers a WHERE a.SessionQuestionId = @SessionQuestionId AND a.ParticipantId = p.ParticipantId);

    UPDATE dbo.SessionQuestions SET RevealedAt = @Now WHERE Id = @SessionQuestionId;

    COMMIT;
    SELECT CAST(1 AS bit) AS Revealed;
END
