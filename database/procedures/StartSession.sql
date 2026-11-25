-- Starts a Workshop's Session in an Open Masterclass with the Host's Difficulty mix (@Easy, @Medium,
-- @Hard Questions), and starts its first Question. Takes time only from @Now and randomness only from
-- @Seed (same seed, same Questions drawn). @EasyLimitMs, @MediumLimitMs and @HardLimitMs are the times
-- to answer per Difficulty, from configuration; each selected Question keeps its own.
--
-- Selection: random within each Difficulty, only current (not retired) Questions of the Workshop, each
-- at most once. A Difficulty that runs short is filled from the next level down (Hard -> Medium -> Easy);
-- a short Easy is filled from the next level up (Medium, then Hard). Questions run from Easy to Hard.
--
-- Result sets: first the problems (one nvarchar per row). If there are any, nothing is stored and that
-- is the only result set. Otherwise a second result set lists the selected Questions in run order.
CREATE OR ALTER PROCEDURE dbo.StartSession
    @MasterclassId int,
    @WorkshopId    int,
    @Easy          int,
    @Medium        int,
    @Hard          int,
    @EasyLimitMs   int,
    @MediumLimitMs int,
    @HardLimitMs   int,
    @Seed          int,
    @Now           datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Problems TABLE (Position int IDENTITY, Problem nvarchar(max) NOT NULL);

    BEGIN TRANSACTION;

    -- Locking the Masterclass row makes its Session starts take turns.
    DECLARE @CourseId int, @Status tinyint;
    SELECT @CourseId = CourseId, @Status = Status
    FROM dbo.Masterclasses WITH (UPDLOCK, HOLDLOCK)
    WHERE Id = @MasterclassId;

    IF @Status IS NULL OR @Status <> 2
    BEGIN
        COMMIT;
        SELECT N'Sessions can only start in an Open Masterclass.' AS Problem;
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Workshops WHERE Id = @WorkshopId AND CourseId = @CourseId AND RetiredAt IS NULL)
    BEGIN
        COMMIT;
        SELECT N'There is no such Workshop in this Masterclass''s Course.' AS Problem;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.Sessions WHERE MasterclassId = @MasterclassId AND WorkshopId = @WorkshopId)
        INSERT @Problems (Problem) VALUES (N'This Workshop has already had its Session in this Masterclass.');
    ELSE IF EXISTS (SELECT 1 FROM dbo.Sessions WHERE MasterclassId = @MasterclassId AND EndedAt IS NULL)
        INSERT @Problems (Problem) VALUES (N'Another Session is still running.');

    -- The Workshop's current Questions, each with a random draw that orders it within its Difficulty.
    DECLARE @Candidates TABLE (QuestionId int PRIMARY KEY, Difficulty tinyint NOT NULL, Draw float NULL);
    INSERT @Candidates (QuestionId, Difficulty)
    SELECT q.Id, q.Difficulty
    FROM dbo.Questions q
    JOIN dbo.Lessons l ON l.Id = q.LessonId
    WHERE l.WorkshopId = @WorkshopId AND q.RetiredAt IS NULL;

    DECLARE @EasyAvailable int = (SELECT COUNT(*) FROM @Candidates WHERE Difficulty = 1);
    DECLARE @MediumAvailable int = (SELECT COUNT(*) FROM @Candidates WHERE Difficulty = 2);
    DECLARE @HardAvailable int = (SELECT COUNT(*) FROM @Candidates WHERE Difficulty = 3);
    DECLARE @Available int = @EasyAvailable + @MediumAvailable + @HardAvailable;

    IF @Easy IS NULL OR @Medium IS NULL OR @Hard IS NULL OR @Easy < 0 OR @Medium < 0 OR @Hard < 0
        INSERT @Problems (Problem) VALUES (N'The Difficulty mix can''t have negative counts.');
    ELSE IF @Easy + @Medium + @Hard = 0
        INSERT @Problems (Problem) VALUES (N'The Difficulty mix needs at least one Question.');
    ELSE IF @Easy + @Medium + @Hard > @Available
        INSERT @Problems (Problem) VALUES (CONCAT(
            N'The Difficulty mix asks for ', @Easy + @Medium + @Hard, N' Questions but the Workshop has only ',
            @Available, N'.'));

    IF EXISTS (SELECT 1 FROM @Problems)
    BEGIN
        COMMIT;
        SELECT Problem FROM @Problems ORDER BY Position;
        RETURN;
    END

    -- How many to take of each Difficulty: a short Hard falls to Medium, a short Medium to Easy, and
    -- a short Easy is filled from what is left of Medium, then of Hard. The total fits (checked above).
    DECLARE @TakeHard int = IIF(@Hard < @HardAvailable, @Hard, @HardAvailable);
    DECLARE @WantMedium int = @Medium + (@Hard - @TakeHard);
    DECLARE @TakeMedium int = IIF(@WantMedium < @MediumAvailable, @WantMedium, @MediumAvailable);
    DECLARE @WantEasy int = @Easy + (@WantMedium - @TakeMedium);
    DECLARE @TakeEasy int = IIF(@WantEasy < @EasyAvailable, @WantEasy, @EasyAvailable);
    DECLARE @EasyGap int = @WantEasy - @TakeEasy;
    DECLARE @FromMedium int = IIF(@EasyGap < @MediumAvailable - @TakeMedium, @EasyGap, @MediumAvailable - @TakeMedium);
    SET @TakeMedium += @FromMedium;
    SET @TakeHard += @EasyGap - @FromMedium;

    -- RAND(@Seed) seeds the sequence; each RAND() after it is the next draw. Drawn in Id order, so the
    -- same seed gives the same draws.
    DECLARE @Ignored float = RAND(@Seed);
    DECLARE @QuestionId int = (SELECT MIN(QuestionId) FROM @Candidates);
    WHILE @QuestionId IS NOT NULL
    BEGIN
        UPDATE @Candidates SET Draw = RAND() WHERE QuestionId = @QuestionId;
        SET @QuestionId = (SELECT MIN(QuestionId) FROM @Candidates WHERE QuestionId > @QuestionId);
    END

    SELECT Problem FROM @Problems; -- empty

    INSERT dbo.Sessions (MasterclassId, WorkshopId, EasyCount, MediumCount, HardCount, StartedAt)
    VALUES (@MasterclassId, @WorkshopId, @Easy, @Medium, @Hard, @Now);
    DECLARE @SessionId int = CAST(SCOPE_IDENTITY() AS int);

    INSERT dbo.SessionQuestions (SessionId, Position, QuestionId, TimeLimitMs)
    SELECT @SessionId,
        ROW_NUMBER() OVER (ORDER BY c.Difficulty, c.Draw, c.QuestionId),
        c.QuestionId,
        CASE c.Difficulty WHEN 1 THEN @EasyLimitMs WHEN 2 THEN @MediumLimitMs ELSE @HardLimitMs END
    FROM (
        SELECT QuestionId, Difficulty, Draw,
            ROW_NUMBER() OVER (PARTITION BY Difficulty ORDER BY Draw, QuestionId) AS Rank
        FROM @Candidates) c
    WHERE c.Rank <= CASE c.Difficulty WHEN 1 THEN @TakeEasy WHEN 2 THEN @TakeMedium ELSE @TakeHard END;

    EXEC dbo.StartNextQuestion @SessionId = @SessionId, @Now = @Now;

    COMMIT;

    SELECT sq.SessionId, sq.Position AS Number, sq.QuestionId, q.StableId, CAST(q.Difficulty AS int) AS Difficulty
    FROM dbo.SessionQuestions sq
    JOIN dbo.Questions q ON q.Id = sq.QuestionId
    WHERE sq.SessionId = @SessionId
    ORDER BY sq.Position;
END
