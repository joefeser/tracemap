-- OUTPUT declares capability; this body does not assign an output value.
CREATE OR ALTER PROCEDURE dbo.ReadItem
    @Name nvarchar(20) OUTPUT
AS
SELECT 1;
SELECT 2;
RETURN 7;
GO
