-- Every Course by title, with its current Workshops in order and their counts of current (not retired)
-- Questions per Difficulty, as one JSON array.
CREATE OR ALTER PROCEDURE dbo.ListCourses
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ISNULL((
        SELECT c.Id AS id, c.Slug AS slug, c.Title AS title,
            JSON_QUERY(ISNULL((
                SELECT w.Id AS id, w.Slug AS slug, w.Title AS title, w.Number AS number,
                    ISNULL(counts.Easy, 0) AS [questions.easy],
                    ISNULL(counts.Medium, 0) AS [questions.medium],
                    ISNULL(counts.Hard, 0) AS [questions.hard]
                FROM dbo.Workshops w
                OUTER APPLY (
                    SELECT SUM(IIF(q.Difficulty = 1, 1, 0)) AS Easy,
                           SUM(IIF(q.Difficulty = 2, 1, 0)) AS Medium,
                           SUM(IIF(q.Difficulty = 3, 1, 0)) AS Hard
                    FROM dbo.Questions q
                    JOIN dbo.Lessons l ON l.Id = q.LessonId
                    WHERE l.WorkshopId = w.Id AND q.RetiredAt IS NULL) counts
                WHERE w.CourseId = c.Id AND w.RetiredAt IS NULL
                ORDER BY w.Number, w.Slug
                FOR JSON PATH), N'[]')) AS workshops
        FROM dbo.Courses c
        ORDER BY c.Title, c.Slug
        FOR JSON PATH), N'[]') AS Courses;
END
