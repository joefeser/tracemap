-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
SELECT i.Id, i.Name, i.Total FROM dbo.Items AS i JOIN dbo.Others AS o ON o.Title = i.Name;
