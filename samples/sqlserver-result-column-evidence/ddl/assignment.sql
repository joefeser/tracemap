-- Public synthetic T-SQL. Parse-only result-shape template; never executed.
CREATE PROCEDURE dbo.ReadItems @Name nvarchar(20) OUTPUT
AS
SELECT @Name = N'synthetic';
