-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
SELECT x.Id, i.Name, i.Total FROM dbo.Items AS i;
