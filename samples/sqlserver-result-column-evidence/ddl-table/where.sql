-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE PROCEDURE dbo.ReadItems
    @Id int
AS
SELECT Id, Name, Total FROM dbo.Items WHERE Id = @Id;
