-- Masterclasses with their Course and number of Participants, newest first.
-- @MasterclassId given: only that one (no row if there is no such Masterclass).
-- Status: 1 Draft, 2 Open, 3 Closed.
CREATE OR ALTER PROCEDURE dbo.ListMasterclasses
    @MasterclassId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT m.Id, m.Name, m.CourseId, c.Title AS CourseTitle, CAST(m.Status AS int) AS Status, m.JoinCode,
           (SELECT COUNT(*) FROM dbo.Participants p WHERE p.MasterclassId = m.Id) AS Participants,
           m.CreatedAt, m.OpenedAt, m.ClosedAt
    FROM dbo.Masterclasses m
    JOIN dbo.Courses c ON c.Id = m.CourseId
    WHERE @MasterclassId IS NULL OR m.Id = @MasterclassId
    ORDER BY m.CreatedAt DESC, m.Id DESC;
END
