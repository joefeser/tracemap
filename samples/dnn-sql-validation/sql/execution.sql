-- Separate synthetic execution checks, never DNN execution or static fact input.
SET NOCOUNT ON;
DECLARE @Rows TABLE (Id int, Name nvarchar(20));
INSERT @Rows EXEC dbo.ReadItem @Name=N'alpha';
IF (SELECT COUNT(*) FROM @Rows WHERE Id=1 AND Name=N'alpha') <> 1
    THROW 51000, 'ReadItem result mismatch', 1;
DECLARE @Count int, @Status int;
EXEC @Status = dbo.Channels @Count=@Count OUTPUT;
IF @Status <> 7 OR @Count <> 1 THROW 51001, 'RETURN or OUTPUT mismatch', 1;
-- Channels emits two result sets; sqlcmd captures them separately in execution.log.
GO
