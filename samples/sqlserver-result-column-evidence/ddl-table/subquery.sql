-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
SELECT d.Id, d.Name, d.Total FROM (SELECT Id, Name, Total FROM dbo.Items) AS d;
