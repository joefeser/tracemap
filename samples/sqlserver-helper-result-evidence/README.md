# Independent SQL Server helper-result consumer fixtures
Compile/scan only; never invoke methods. No database or runtime values are needed.
C# and VB each have 32 `SqlDataAdapter.Fill` sites: five bounded private helpers (`ReadTable`,
`ReadSet`, `ReadNamedSet`, `ReadTableText`, `ReadTableUsing`), 25 refused or uncalled helpers,
`InlineFill` and the second Fill inside `CallerRefill`. Method names match across languages;
each refused helper keeps one `Use<Helper>` caller.
Result shapes are reused from [`../sqlserver-result-column-evidence`](../sqlserver-result-column-evidence/README.md)
(JSON base declarations, `ddl/` and `ddl-table/` templates). See
[the contract, syntax matrix and native commands](../../docs/validation/SQLSERVER_HELPER_RESULT_EVIDENCE.md).
