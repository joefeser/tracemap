# Independent SQL Server DataSet consumer fixtures
Compile/scan only; never invoke methods. No database or runtime values are needed.
C# and VB each have 96 methods / 99 `SqlDataAdapter.Fill` sites (TwoFills, RepeatedFill and
AdapterReused have two) plus one FillSchema site. Method names match across languages; VB
`BangName` uses `results.Tables!Table` and `row!Name`.
Result shapes are reused from [`../sqlserver-result-column-evidence`](../sqlserver-result-column-evidence/README.md)
(JSON base declarations, `ddl/` and `ddl-table/` templates). See
[the contract, syntax matrix and native commands](../../docs/validation/SQLSERVER_DATASET_EVIDENCE.md).
