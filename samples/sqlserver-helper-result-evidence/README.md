# Independent SQL Server helper-result consumer fixtures
Compile/scan only; never invoke methods. No database or runtime values are needed.
C# and VB each have 32 `SqlDataAdapter.Fill` sites: five bounded private helpers (`ReadTable`,
`ReadSet`, `ReadNamedSet`, `ReadTableText`, `ReadTableUsing`), 25 refused or uncalled helpers,
`InlineFill` and the second Fill inside `CallerRefill`. Method names match across languages;
each refused helper keeps one `Use<Helper>` caller.
Result shapes are reused from [`../sqlserver-result-column-evidence`](../sqlserver-result-column-evidence/README.md)
(JSON base declarations, `ddl/` and `ddl-table/` templates). See
[the contract, syntax matrix and native commands](../../docs/validation/SQLSERVER_HELPER_RESULT_EVIDENCE.md).

## Security analyzer boundary

The C# fixture's 29 rule-specific `nosemgrep` annotations apply only to
`csharp.lang.security.sqli.csharp-sqli`. These are deliberately varied source-analysis
inputs, compiled and scanned but never invoked; altering their command construction
would remove coverage. The fixture project is a library, not an executable.
This is not a repository-wide SQL-injection exclusion or guidance for application SQL.
