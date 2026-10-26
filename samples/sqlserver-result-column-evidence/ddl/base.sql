-- Public synthetic T-SQL. Parse-only result-shape template; never executed.
CREATE PROCEDURE [dbo].[ReadItems]
AS
BEGIN
    SELECT CAST(1 AS int) AS Id,
        N'synthetic' AS [Name],
        CAST(2.50 AS decimal(10,2)) AS Total;
END;
GO
CREATE PROCEDURE dbo.ReadOther
AS
SELECT N'synthetic' AS Title;
GO
