-- Metadata description only: errors/conditional results remain unavailable.
SELECT SERVERPROPERTY('ProductVersion') AS ProductVersion,
       SERVERPROPERTY('Edition') AS Edition;
SELECT p.name, TYPE_NAME(p.user_type_id) AS TypeName, p.max_length, p.is_output
FROM sys.parameters p WHERE p.object_id = OBJECT_ID(N'dbo.ReadItem');
EXEC sys.sp_describe_first_result_set @tsql=N'EXEC dbo.ReadItem @Name=N''alpha''';
GO
