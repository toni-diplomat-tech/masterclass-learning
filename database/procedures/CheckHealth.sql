-- Trivial round trip used by the health page. Takes time only from @Now.
CREATE OR ALTER PROCEDURE dbo.CheckHealth
    @Now datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @Now AS CheckedAtUtc;
END
