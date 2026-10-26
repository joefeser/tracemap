-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
EXEC sys.sp_executesql N'SELECT Id, Name, Total FROM dbo.Items';
