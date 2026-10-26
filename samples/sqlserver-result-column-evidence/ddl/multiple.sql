-- Public synthetic T-SQL. Parse-only result-shape template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
BEGIN
    SELECT 1 AS Id, N'synthetic' AS Name, 2 AS Total;
    SELECT 1 AS Extra;
END;
