-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
BEGIN
    SELECT Id, Name, Total FROM dbo.Items;
    SELECT Title FROM dbo.Others;
END;
