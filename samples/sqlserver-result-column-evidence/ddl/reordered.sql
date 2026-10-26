-- Public synthetic T-SQL. Parse-only result-shape template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
SELECT CAST(2.50 AS decimal(10,2)) AS Total, N'synthetic' AS Name, CAST(1 AS int) AS Id;
