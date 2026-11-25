-- One Course's current Workshops and Lessons in order, each Lesson with all its Questions (retired ones
-- flagged) and their current options, as one JSON document. NULL when there is no such Course.
-- Wrapped in a scalar subquery so the JSON comes back as one value instead of 2 KB chunks.
CREATE OR ALTER PROCEDURE dbo.GetCourse
    @CourseSlug nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT (
        SELECT c.Id AS id, c.Slug AS slug, c.Title AS title,
            JSON_QUERY(ISNULL((
                SELECT w.Id AS id, w.Slug AS slug, w.Title AS title, w.Number AS number,
                    JSON_QUERY(ISNULL((
                        SELECT l.Id AS id, l.Slug AS slug, l.Title AS title, l.Number AS number, l.Url AS url,
                            JSON_QUERY(ISNULL((
                                SELECT q.Id AS id, q.StableId AS stableId, q.Difficulty AS difficulty, q.Text AS text,
                                    CAST(IIF(q.RetiredAt IS NULL, 0, 1) AS bit) AS retired,
                                    JSON_QUERY(ISNULL((
                                        SELECT o.Position AS position, o.Text AS text, o.IsCorrect AS correct
                                        FROM dbo.QuestionOptions o
                                        WHERE o.QuestionId = q.Id AND o.RetiredAt IS NULL
                                        ORDER BY o.Position
                                        FOR JSON PATH), N'[]')) AS options
                                FROM dbo.Questions q
                                WHERE q.LessonId = l.Id
                                ORDER BY q.StableId
                                FOR JSON PATH), N'[]')) AS questions
                        FROM dbo.Lessons l
                        WHERE l.WorkshopId = w.Id AND l.RetiredAt IS NULL
                        ORDER BY l.Number, l.Slug
                        FOR JSON PATH), N'[]')) AS lessons
                FROM dbo.Workshops w
                WHERE w.CourseId = c.Id AND w.RetiredAt IS NULL
                ORDER BY w.Number, w.Slug
                FOR JSON PATH), N'[]')) AS workshops
        FROM dbo.Courses c
        WHERE c.Slug = @CourseSlug
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS Course;
END
