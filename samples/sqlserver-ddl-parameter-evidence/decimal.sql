-- Precision/scale are retained, with explicit unsupported semantics.
CREATE PROCEDURE dbo.ReadItem
    @Name decimal(10,2)
AS SELECT 1;
GO
