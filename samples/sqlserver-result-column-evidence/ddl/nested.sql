-- Public synthetic T-SQL. Parse-only result-shape template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
BEGIN
    IF 1 = 1
    BEGIN
        SELECT 1 AS Id, N'synthetic' AS Name, 2 AS Total;
    END
END;
