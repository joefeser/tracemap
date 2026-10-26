-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE TABLE dbo.Items (
    Id int NOT NULL,
    Name nvarchar(50) NOT NULL,
    Total decimal(10,2) NULL,
    Label AS (Name)
);
GO
