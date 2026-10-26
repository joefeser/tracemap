-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
AS
BEGIN
    SELECT Id,
        Name,
        Total
    FROM dbo.Items;
END;
GO
CREATE PROCEDURE dbo.ReadOther
AS
SELECT o.Title FROM dbo.Others AS o;
GO
