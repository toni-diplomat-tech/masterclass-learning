-- Creates a Draft Masterclass for a Course. Takes time only from @Now.
--
-- Result sets: first the problems (one nvarchar per row). If there are any, nothing is stored and that
-- is the only result set. Otherwise a second result set has the new Masterclass's Id.
CREATE OR ALTER PROCEDURE dbo.CreateMasterclass
    @CourseId int,
    @Name     nvarchar(max),
    @Now      datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Problems TABLE (Position int IDENTITY, Problem nvarchar(max) NOT NULL);
    SET @Name = NULLIF(TRIM(@Name), N'');

    IF @Name IS NULL
        INSERT @Problems (Problem) VALUES (N'The Masterclass needs a name.');
    ELSE IF LEN(@Name) > 200
        INSERT @Problems (Problem) VALUES (N'The Masterclass name is longer than 200 characters.');

    IF NOT EXISTS (SELECT 1 FROM dbo.Courses WHERE Id = @CourseId)
        INSERT @Problems (Problem) VALUES (N'There is no such Course.');

    IF EXISTS (SELECT 1 FROM @Problems)
    BEGIN
        SELECT Problem FROM @Problems ORDER BY Position;
        RETURN;
    END

    SELECT Problem FROM @Problems; -- empty
    INSERT dbo.Masterclasses (CourseId, Name, Status, CreatedAt)
    VALUES (@CourseId, @Name, 1, @Now);
    SELECT CAST(SCOPE_IDENTITY() AS int) AS Id;
END
