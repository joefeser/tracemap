-- Public synthetic T-SQL. Parse-only table-backed result template; never executed.
CREATE TABLE dbo.Items (
    Id int NOT NULL PRIMARY KEY,
    Name nvarchar(50) NOT NULL,
    Total decimal(10,2) NULL,
    Price decimal(10,2) NULL,
    ItemCode nvarchar(20) NOT NULL
);
GO
CREATE TABLE [dbo].[Others] (
    [Title] nvarchar(40) NOT NULL,
    CONSTRAINT UQ_Others_Title UNIQUE (Title)
);
GO
