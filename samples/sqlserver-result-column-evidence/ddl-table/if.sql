-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
IF 1 = 1
    SELECT Id, Name, Total FROM dbo.Items;
