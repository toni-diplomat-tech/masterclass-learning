-- The Next Workshop of a Masterclass: the first current (not retired) Workshop of its Course, in Course
-- order (Number, then Id), without a Session in the Masterclass. A started Session counts, so while it
-- runs the Next Workshop is the one after it. No row once every current Workshop has had its Session.
CREATE OR ALTER FUNCTION dbo.NextWorkshop (@MasterclassId int)
RETURNS TABLE
AS
RETURN
    SELECT TOP (1) w.Id, w.Title
    FROM dbo.Masterclasses m
    JOIN dbo.Workshops w ON w.CourseId = m.CourseId
    WHERE m.Id = @MasterclassId AND w.RetiredAt IS NULL
        AND NOT EXISTS (SELECT 1 FROM dbo.Sessions s WHERE s.MasterclassId = m.Id AND s.WorkshopId = w.Id)
    ORDER BY w.Number, w.Id;
