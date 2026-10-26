-- Public synthetic T-SQL. Parse-only result-shape template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
SELECT CAST(1 AS int) AS Id, N'synthetic' AS DisplayName, CAST(2.50 AS decimal(10,2)) AS Total;
