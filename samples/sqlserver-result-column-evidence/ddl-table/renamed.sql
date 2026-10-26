-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
SELECT Id, Name AS DisplayName, Total FROM dbo.Items;
