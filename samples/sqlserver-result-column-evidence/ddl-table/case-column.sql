-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
SELECT Id, name, Total FROM dbo.Items;
