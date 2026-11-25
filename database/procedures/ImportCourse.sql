-- Creates or updates a Course from an import file (docs/import-file.md), as JSON normalized by the
-- Quiz engine. Takes time only from @Now.
--
-- Result sets: first the problems (one nvarchar per row). If there are any, nothing is stored and that
-- is the only result set. Otherwise a second result set has one summary row.
--
-- Upsert rules: Workshops are keyed on (Course, slug), Lessons and Questions on (Course, slug/id), and
-- options on (Question, position). Text, titles, numbers, URLs and Difficulty are updated in place.
-- Whatever is missing from the file is retired (RetiredAt = @Now), never deleted, so past Answers keep
-- their Questions; whatever comes back is restored.
CREATE OR ALTER PROCEDURE dbo.ImportCourse
    @File nvarchar(max),
    @Now  datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Slugs and ids match case-insensitively, like their unique constraints. Change detection compares
    -- with a binary collation, so an edit that only changes case is still stored.

    ---------------------------------------------------------------------------------------------------
    -- Read the file into staging tables. Blank strings count as missing.
    ---------------------------------------------------------------------------------------------------
    DECLARE @CourseSlug nvarchar(max), @CourseTitle nvarchar(max);
    SELECT @CourseSlug = NULLIF(TRIM(Slug), N''), @CourseTitle = NULLIF(TRIM(Title), N'')
    FROM OPENJSON(@File, '$.course') WITH (Slug nvarchar(max) '$.slug', Title nvarchar(max) '$.title');

    CREATE TABLE #Workshops (
        Position int NOT NULL,
        Slug     nvarchar(max) NULL,
        Title    nvarchar(max) NULL,
        Number   int NULL,
        Lessons  nvarchar(max) NULL,
        Label    AS COALESCE(N'Workshop ''' + Slug + N'''', N'Workshop #' + CAST(Position AS nvarchar(10)))
    );
    INSERT #Workshops (Position, Slug, Title, Number, Lessons)
    SELECT CAST(w.[key] AS int) + 1, NULLIF(TRIM(x.Slug), N''), NULLIF(TRIM(x.Title), N''), x.Number, x.Lessons
    FROM OPENJSON(@File, '$.workshops') w
    OUTER APPLY OPENJSON(w.value) WITH (
        Slug    nvarchar(max) '$.slug',
        Title   nvarchar(max) '$.title',
        Number  int           '$.number',
        Lessons nvarchar(max) '$.lessons' AS JSON) x;

    CREATE TABLE #Lessons (
        WorkshopPosition int NOT NULL,
        Position         int NOT NULL,
        WorkshopSlug     nvarchar(max) NULL,
        Slug             nvarchar(max) NULL,
        Title            nvarchar(max) NULL,
        Number           int NULL,
        Url              nvarchar(max) NULL,
        Label            nvarchar(max) NULL
    );
    INSERT #Lessons (WorkshopPosition, Position, WorkshopSlug, Slug, Title, Number, Url, Label)
    SELECT w.Position, CAST(l.[key] AS int) + 1, w.Slug, s.Slug, NULLIF(TRIM(x.Title), N''), x.Number, NULLIF(TRIM(x.Url), N''),
           COALESCE(N'Lesson ''' + s.Slug + N'''', N'Lesson #' + CAST(CAST(l.[key] AS int) + 1 AS nvarchar(10)) + N' of ' + w.Label)
    FROM #Workshops w
    CROSS APPLY OPENJSON(w.Lessons) l
    OUTER APPLY OPENJSON(l.value) WITH (
        Slug   nvarchar(max) '$.slug',
        Title  nvarchar(max) '$.title',
        Number int           '$.number',
        Url    nvarchar(max) '$.url') x
    CROSS APPLY (SELECT NULLIF(TRIM(x.Slug), N'') AS Slug) s;

    CREATE TABLE #Questions (
        Position   int NOT NULL,
        StableId   nvarchar(max) NULL,
        LessonSlug nvarchar(max) NULL,
        Difficulty nvarchar(max) NULL,
        Text       nvarchar(max) NULL,
        Options    nvarchar(max) NULL,
        Label      AS COALESCE(N'Question ''' + StableId + N'''', N'Question #' + CAST(Position AS nvarchar(10))),
        -- Filled in once the file is valid:
        DifficultyLevel tinyint NULL,
        LessonId   int NULL,
        QuestionId int NULL,
        Changed    bit NOT NULL DEFAULT 0
    );
    INSERT #Questions (Position, StableId, LessonSlug, Difficulty, Text, Options)
    SELECT CAST(q.[key] AS int) + 1, NULLIF(TRIM(x.Id), N''), NULLIF(TRIM(x.Lesson), N''), NULLIF(TRIM(x.Difficulty), N''),
           NULLIF(TRIM(x.Text), N''), x.Options
    FROM OPENJSON(@File, '$.questions') q
    OUTER APPLY OPENJSON(q.value) WITH (
        Id         nvarchar(max) '$.id',
        Lesson     nvarchar(max) '$.lesson',
        Difficulty nvarchar(max) '$.difficulty',
        Text       nvarchar(max) '$.text',
        Options    nvarchar(max) '$.options' AS JSON) x;

    CREATE TABLE #Options (
        QuestionPosition int NOT NULL,
        Position         int NOT NULL,
        Text             nvarchar(max) NULL,
        IsCorrect        bit NOT NULL
    );
    INSERT #Options (QuestionPosition, Position, Text, IsCorrect)
    SELECT q.Position, CAST(o.[key] AS int) + 1, NULLIF(TRIM(x.Text), N''), COALESCE(x.Correct, 0)
    FROM #Questions q
    CROSS APPLY OPENJSON(q.Options) o
    OUTER APPLY OPENJSON(o.value) WITH (Text nvarchar(max) '$.text', Correct bit '$.correct') x;

    ---------------------------------------------------------------------------------------------------
    -- Validate. Every problem is reported, in file order; any problem rejects the whole file.
    ---------------------------------------------------------------------------------------------------
    DECLARE @Problems TABLE (Seq int IDENTITY PRIMARY KEY, Problem nvarchar(max) NOT NULL);

    IF EXISTS (SELECT 1 FROM @Problems)
    BEGIN
        SELECT Problem FROM @Problems ORDER BY Seq;
        RETURN;
    END;

    ---------------------------------------------------------------------------------------------------
    -- Upsert.
    ---------------------------------------------------------------------------------------------------
    BEGIN TRANSACTION;

    DECLARE @CourseId int = (SELECT Id FROM dbo.Courses WITH (UPDLOCK, HOLDLOCK) WHERE Slug = @CourseSlug);
    IF @CourseId IS NULL
    BEGIN
        INSERT dbo.Courses (Slug, Title, CreatedAt) VALUES (@CourseSlug, @CourseTitle, @Now);
        SET @CourseId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.Courses SET Slug = @CourseSlug, Title = @CourseTitle
        WHERE Id = @CourseId
          AND (Slug <> @CourseSlug COLLATE Latin1_General_100_BIN2 OR Title <> @CourseTitle COLLATE Latin1_General_100_BIN2);
    END;

    -- Workshops
    UPDATE w SET Slug = s.Slug, Title = s.Title, Number = s.Number, RetiredAt = NULL
    FROM dbo.Workshops w
    JOIN #Workshops s ON w.CourseId = @CourseId AND w.Slug = s.Slug
    WHERE w.Slug <> s.Slug COLLATE Latin1_General_100_BIN2
       OR w.Title <> s.Title COLLATE Latin1_General_100_BIN2
       OR w.Number <> s.Number
       OR w.RetiredAt IS NOT NULL;

    INSERT dbo.Workshops (CourseId, Slug, Title, Number)
    SELECT @CourseId, s.Slug, s.Title, s.Number
    FROM #Workshops s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.CourseId = @CourseId AND w.Slug = s.Slug);

    UPDATE w SET RetiredAt = @Now
    FROM dbo.Workshops w
    WHERE w.CourseId = @CourseId AND w.RetiredAt IS NULL
      AND NOT EXISTS (SELECT 1 FROM #Workshops s WHERE s.Slug = w.Slug);

    -- Lessons
    UPDATE l SET WorkshopId = w.Id, Slug = s.Slug, Title = s.Title, Number = s.Number, Url = s.Url, RetiredAt = NULL
    FROM dbo.Lessons l
    JOIN #Lessons s ON l.CourseId = @CourseId AND l.Slug = s.Slug
    JOIN dbo.Workshops w ON w.CourseId = @CourseId AND w.Slug = s.WorkshopSlug
    WHERE l.WorkshopId <> w.Id
       OR l.Slug <> s.Slug COLLATE Latin1_General_100_BIN2
       OR l.Title <> s.Title COLLATE Latin1_General_100_BIN2
       OR l.Number <> s.Number
       OR l.Url <> s.Url COLLATE Latin1_General_100_BIN2
       OR l.RetiredAt IS NOT NULL;

    INSERT dbo.Lessons (CourseId, WorkshopId, Slug, Title, Number, Url)
    SELECT @CourseId, w.Id, s.Slug, s.Title, s.Number, s.Url
    FROM #Lessons s
    JOIN dbo.Workshops w ON w.CourseId = @CourseId AND w.Slug = s.WorkshopSlug
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Lessons l WHERE l.CourseId = @CourseId AND l.Slug = s.Slug);

    UPDATE l SET RetiredAt = @Now
    FROM dbo.Lessons l
    WHERE l.CourseId = @CourseId AND l.RetiredAt IS NULL
      AND NOT EXISTS (SELECT 1 FROM #Lessons s WHERE s.Slug = l.Slug);

    -- Questions: work out which existing ones change before touching them, so the summary can count them.
    UPDATE q SET
        DifficultyLevel = CASE q.Difficulty WHEN N'Easy' THEN 1 WHEN N'Medium' THEN 2 WHEN N'Hard' THEN 3 END,
        LessonId = l.Id,
        QuestionId = existing.Id
    FROM #Questions q
    JOIN dbo.Lessons l ON l.CourseId = @CourseId AND l.Slug = q.LessonSlug
    LEFT JOIN dbo.Questions existing ON existing.CourseId = @CourseId AND existing.StableId = q.StableId;

    UPDATE q SET Changed = 1
    FROM #Questions q
    JOIN dbo.Questions existing ON existing.Id = q.QuestionId
    WHERE existing.StableId <> q.StableId COLLATE Latin1_General_100_BIN2
       OR existing.LessonId <> q.LessonId
       OR existing.Difficulty <> q.DifficultyLevel
       OR existing.Text <> q.Text COLLATE Latin1_General_100_BIN2
       OR existing.RetiredAt IS NOT NULL
       -- an option added, edited or restored...
       OR EXISTS (
            SELECT 1 FROM #Options s
            LEFT JOIN dbo.QuestionOptions o ON o.QuestionId = q.QuestionId AND o.Position = s.Position
            WHERE s.QuestionPosition = q.Position
              AND (o.Id IS NULL
                   OR o.Text <> s.Text COLLATE Latin1_General_100_BIN2
                   OR o.IsCorrect <> s.IsCorrect
                   OR o.RetiredAt IS NOT NULL))
       -- ...or removed.
       OR EXISTS (
            SELECT 1 FROM dbo.QuestionOptions o
            WHERE o.QuestionId = q.QuestionId AND o.RetiredAt IS NULL
              AND NOT EXISTS (SELECT 1 FROM #Options s WHERE s.QuestionPosition = q.Position AND s.Position = o.Position));

    DECLARE @QuestionsUpdated int = (SELECT COUNT(*) FROM #Questions WHERE Changed = 1);
    DECLARE @QuestionsAdded int = (SELECT COUNT(*) FROM #Questions WHERE QuestionId IS NULL);

    UPDATE existing SET StableId = q.StableId, LessonId = q.LessonId, Difficulty = q.DifficultyLevel, Text = q.Text, RetiredAt = NULL
    FROM dbo.Questions existing
    JOIN #Questions q ON q.QuestionId = existing.Id
    WHERE q.Changed = 1;

    INSERT dbo.Questions (CourseId, StableId, LessonId, Difficulty, Text)
    SELECT @CourseId, q.StableId, q.LessonId, q.DifficultyLevel, q.Text
    FROM #Questions q
    WHERE q.QuestionId IS NULL;

    UPDATE q SET QuestionId = existing.Id
    FROM #Questions q
    JOIN dbo.Questions existing ON existing.CourseId = @CourseId AND existing.StableId = q.StableId
    WHERE q.QuestionId IS NULL;

    UPDATE existing SET RetiredAt = @Now
    FROM dbo.Questions existing
    WHERE existing.CourseId = @CourseId AND existing.RetiredAt IS NULL
      AND NOT EXISTS (SELECT 1 FROM #Questions q WHERE q.QuestionId = existing.Id);
    DECLARE @QuestionsRetired int = @@ROWCOUNT;

    -- Options of the Questions in the file. Options of retired Questions stay as they were.
    UPDATE o SET Text = s.Text, IsCorrect = s.IsCorrect, RetiredAt = NULL
    FROM dbo.QuestionOptions o
    JOIN #Questions q ON q.QuestionId = o.QuestionId
    JOIN #Options s ON s.QuestionPosition = q.Position AND s.Position = o.Position
    WHERE o.Text <> s.Text COLLATE Latin1_General_100_BIN2
       OR o.IsCorrect <> s.IsCorrect
       OR o.RetiredAt IS NOT NULL;

    INSERT dbo.QuestionOptions (QuestionId, Position, Text, IsCorrect)
    SELECT q.QuestionId, s.Position, s.Text, s.IsCorrect
    FROM #Options s
    JOIN #Questions q ON q.Position = s.QuestionPosition
    WHERE NOT EXISTS (SELECT 1 FROM dbo.QuestionOptions o WHERE o.QuestionId = q.QuestionId AND o.Position = s.Position);

    UPDATE o SET RetiredAt = @Now
    FROM dbo.QuestionOptions o
    JOIN #Questions q ON q.QuestionId = o.QuestionId
    WHERE o.RetiredAt IS NULL
      AND NOT EXISTS (SELECT 1 FROM #Options s WHERE s.QuestionPosition = q.Position AND s.Position = o.Position);

    COMMIT TRANSACTION;

    SELECT CAST(NULL AS nvarchar(max)) AS Problem WHERE 1 = 0;

    SELECT
        @CourseId AS CourseId,
        @CourseSlug AS CourseSlug,
        @CourseTitle AS CourseTitle,
        (SELECT COUNT(*) FROM #Workshops) AS Workshops,
        (SELECT COUNT(*) FROM #Lessons) AS Lessons,
        (SELECT COUNT(*) FROM #Questions) AS Questions,
        @QuestionsAdded AS QuestionsAdded,
        @QuestionsUpdated AS QuestionsUpdated,
        @QuestionsRetired AS QuestionsRetired;
END
