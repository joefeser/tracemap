-- Public synthetic T-SQL. Parse-only result-shape template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
IF 1 = 1
    SELECT 1 AS Id, N'synthetic' AS Name, 2 AS Total;
