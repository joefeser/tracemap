# Independent SQL Server DataTable consumer fixtures
Compile/scan only; never invoke methods. No database or runtime values are needed.
C# and VB each have 89 methods / 92 `SqlDataAdapter.Fill` sites (TwoFills, RepeatedFill and
AdapterReused have two) plus three other execution sites (an ExecuteNonQuery, an ExecuteReader
gate and a FillSchema). Method names match across languages; VB `BangName` uses `row!Name`.
Result shapes are reused from [`../sqlserver-result-column-evidence`](../sqlserver-result-column-evidence/README.md)
(JSON base declarations, `ddl/` and `ddl-table/` templates). See
[the contract, syntax matrix and native commands](../../docs/validation/SQLSERVER_DATATABLE_EVIDENCE.md).
