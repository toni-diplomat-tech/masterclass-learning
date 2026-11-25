-- A Participant (by their browser's @Token) answers @QuestionId with option @OptionPosition. The answer
-- counts only while that Question is running in the Participant's Masterclass and @Now is no later
-- than its AnswersCloseAt (deadline + grace), even if the reveal is late. Only Participants who were
-- in the Masterclass when the Question started can answer it, once, without changing it.
-- The option is judged against the options the Question started with (dbo.SessionQuestionOptions).
-- Takes time only from @Now. The Answer's points are scored here (see Scoring below).
--
-- Returns one row: Result (Accepted, TooLate, AlreadyAnswered, JoinedAfterStart, UnknownOption,
-- NoSuchQuestion, UnknownParticipant) and, when the Question was found, MasterclassId, SessionId,
-- QuestionId, Answered (Participants who have answered it) and Participants (who can answer it).
CREATE OR ALTER PROCEDURE dbo.SubmitAnswer
    @Token          varchar(200),
    @QuestionId     int,
    @OptionPosition int,
    @Now            datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ParticipantId int, @MasterclassId int, @JoinedAt datetime2(7);
    SELECT @ParticipantId = Id, @MasterclassId = MasterclassId, @JoinedAt = JoinedAt
    FROM dbo.Participants
    WHERE TokenHash = HASHBYTES('SHA2_256', @Token);

    IF @ParticipantId IS NULL
    BEGIN
        SELECT N'UnknownParticipant' AS Result;
        RETURN;
    END

    BEGIN TRANSACTION;

    -- Locking the Session Question makes its Answers and its reveal take turns.
    DECLARE @SessionQuestionId int, @SessionId int, @StartedAt datetime2(7), @AnswersCloseAt datetime2(7),
            @RevealedAt datetime2(7), @TimeLimitMs int;
    SELECT TOP (1)
        @SessionQuestionId = sq.Id, @SessionId = sq.SessionId, @StartedAt = sq.StartedAt,
        @AnswersCloseAt = sq.AnswersCloseAt, @RevealedAt = sq.RevealedAt, @TimeLimitMs = sq.TimeLimitMs
    FROM dbo.SessionQuestions sq WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.Sessions s ON s.Id = sq.SessionId
    WHERE s.MasterclassId = @MasterclassId AND sq.QuestionId = @QuestionId AND sq.StartedAt IS NOT NULL
    ORDER BY sq.StartedAt DESC;

    IF @SessionQuestionId IS NULL
    BEGIN
        COMMIT;
        SELECT N'NoSuchQuestion' AS Result;
        RETURN;
    END

    DECLARE @Result nvarchar(30) =
        CASE
            WHEN @RevealedAt IS NOT NULL OR @Now > @AnswersCloseAt THEN N'TooLate'
            WHEN @JoinedAt > @StartedAt THEN N'JoinedAfterStart'
            WHEN EXISTS (SELECT 1 FROM dbo.Answers WHERE SessionQuestionId = @SessionQuestionId AND ParticipantId = @ParticipantId)
                THEN N'AlreadyAnswered'
            WHEN NOT EXISTS (
                SELECT 1 FROM dbo.SessionQuestionOptions
                WHERE SessionQuestionId = @SessionQuestionId AND Position = @OptionPosition)
                THEN N'UnknownOption'
            ELSE N'Accepted'
        END;

    IF @Result = N'Accepted'
    BEGIN
        -- Scoring: a Correct Answer scores base x (1 - 0.5 x elapsed / limit), rounded, with base points
        -- Easy 500, Medium 750, Hard 1000. Incorrect scores 0. An Answer in the grace period counts as
        -- answered at the limit, so a Correct Answer never scores below half the base points.
        DECLARE @ElapsedMs int = DATEDIFF(millisecond, @StartedAt, @Now);
        DECLARE @ScoredMs int = CASE WHEN @ElapsedMs > @TimeLimitMs THEN @TimeLimitMs ELSE @ElapsedMs END;
        INSERT dbo.Answers (SessionQuestionId, ParticipantId, Outcome, OptionPosition, AnsweredAt, ElapsedMs, Points)
        SELECT @SessionQuestionId, @ParticipantId, CASE WHEN o.IsCorrect = 1 THEN 1 ELSE 2 END, o.Position, @Now,
               @ElapsedMs,
               CASE WHEN o.IsCorrect = 1
                    THEN CAST(ROUND(
                        CASE q.Difficulty WHEN 1 THEN 500 WHEN 2 THEN 750 ELSE 1000 END
                        * (1 - 0.5 * CAST(@ScoredMs AS decimal(19, 6)) / @TimeLimitMs), 0) AS int)
                    ELSE 0 END
        FROM dbo.SessionQuestionOptions o
        JOIN dbo.Questions q ON q.Id = @QuestionId
        WHERE o.SessionQuestionId = @SessionQuestionId AND o.Position = @OptionPosition;
    END

    COMMIT;

    SELECT @Result AS Result, @MasterclassId AS MasterclassId, @SessionId AS SessionId, @QuestionId AS QuestionId,
           (SELECT COUNT(*) FROM dbo.Answers WHERE SessionQuestionId = @SessionQuestionId AND Outcome <> 3) AS Answered,
           (SELECT COUNT(*) FROM dbo.Participants WHERE MasterclassId = @MasterclassId AND JoinedAt <= @StartedAt) AS Participants;
END
