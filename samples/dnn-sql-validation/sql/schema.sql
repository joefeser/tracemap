-- Independently authored; only the disposable harness database may admit this DDL.
CREATE TABLE dbo.Items (Id int NOT NULL PRIMARY KEY, Name nvarchar(20) NOT NULL);
GO
CREATE PROCEDURE dbo.ReadItem @Name nvarchar(20) AS
SELECT Id, Name FROM dbo.Items WHERE Name = @Name;
GO
CREATE PROCEDURE dbo.RenamedItem @Name nvarchar(20) AS
SELECT Id, Name AS DisplayName FROM dbo.Items WHERE Name = @Name;
GO
CREATE PROCEDURE dbo.ConditionalItem @Name nvarchar(20) AS
IF @Name = N'wide' SELECT Id, Name FROM dbo.Items;
ELSE SELECT Id FROM dbo.Items;
GO
CREATE PROCEDURE dbo.DynamicItem @Name nvarchar(20) AS
EXEC sys.sp_executesql N'SELECT Name FROM dbo.Items';
GO
CREATE PROCEDURE dbo.Channels @Count int OUTPUT AS
SET NOCOUNT ON;
SELECT @Count = COUNT(*) FROM dbo.Items;
SELECT Id, Name FROM dbo.Items;
SELECT @Count AS Total;
RETURN 7;
GO
