-- Default semantics are explicitly unsupported; the expression is never evaluated.
CREATE PROCEDURE dbo.ReadItem
    @Name nvarchar(20) = NULL
AS SELECT 1;
GO
