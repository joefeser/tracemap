# Independent SQL Server Field<T> read fixtures
Compile/scan only; never invoke methods. No database or runtime values are needed.
C# and VB each have 26 `SqlDataAdapter.Fill` sites over four documents of one project:
18 direct `Fill(DataTable)` methods in `Tables`, four direct `Fill(DataSet)` methods in `Sets`,
and four helpers in `Helpers` — the private `ReadPrivate` (callers in the same type) and
`ReadBody` (a Field read inside its body), and the internal / `Friend` `ReadShared` and
`ReadSharedSet`, whose callers live in `Callers`. Method names match across languages.
Every `Field<T>` / `Field(Of T)` read here is an authored cell of the matrix in
[the spec design](../../.kiro/specs/sqlserver-field-reads/design.md); the Phase 2-5 states keep
refusing them as `datarow-field-extension-unsupported`.
Result shapes are reused from [`../sqlserver-result-column-evidence`](../sqlserver-result-column-evidence/README.md).
See [the contract, syntax matrix and native commands](../../docs/validation/SQLSERVER_FIELD_READ_EVIDENCE.md).
## Security analyzer boundary
The C# fixture's four rule-specific `nosemgrep` annotations apply only to
`csharp.lang.security.sqli.csharp-sqli` on the helpers' `new SqlCommand(procedure)` lines. These
are deliberately varied source-analysis inputs, compiled and scanned but never invoked. The
fixture projects are libraries, not executables. This is not a repository-wide SQL-injection
exclusion or guidance for application SQL.
