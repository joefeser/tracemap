-- Public synthetic ALTER is one declaration, not a migration to apply.
ALTER PROCEDURE "dbo"."ReadItem"
    @Name nvarchar(MAX)
AS SELECT 1;
GO
