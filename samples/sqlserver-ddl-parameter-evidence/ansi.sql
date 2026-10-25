-- varchar bytes are not silently equated with provider ANSI length policy.
CREATE PROCEDURE dbo.ReadItem
    @Name varchar(20)
AS SELECT 1;
GO
