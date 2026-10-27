# Authored static/runtime matrix

Author before the first scanner run. Both languages use Microsoft.Data.SqlClient
6.1.7 on .NET 10, and the same methods are scanned and executed. Runtime calls use
only the owned synthetic connection. Independent JSON declarations describe the
first result of dbo.ReadItems: Id, Name, Total, and of dbo.ReadOther: Title.
Runtime setup returns those three ReadItems columns followed by a second result
with Value=99, and one ReadOther row with Title=other. No DDL completeness is
claimed for that multiple-SELECT body.

| Method | Route | Static expected | Runtime expected |
| --- | --- | --- | --- |
| ReaderName | reader | bounded-match | sample |
| TableName | DataTable | bounded-match | sample |
| SetName | DataSet | bounded-match | sample |
| MissingName | DataTable | mismatch | ArgumentException |
| CaseOnly | DataTable | unknown | sample (runtime case fallback) |
| LaterTable | DataSet | unknown | 99 (outside first-result scope) |
| HelperTableName | helper (ReadTable, dbo.ReadItems) | bounded-match | sample |
| HelperOtherProcedure | helper (ReadTable, dbo.ReadOther) | bounded-match | other |
| HelperSetName | helper (ReadSet, dbo.ReadItems) | bounded-match | sample |
| HelperMissingName | helper (ReadTable, dbo.ReadItems) | mismatch | ArgumentException |
| HelperLaterTable | helper (ReadSet, dbo.ReadItems) | unknown (`dataset-later-table-unsupported`) | 99 (outside first-result scope) |
| FieldTableName | field (DataTable, `Field<string>("Name")`) | bounded-match | sample |
| FieldOrdinal | field (DataTable, `Field<int>(0)`) | bounded-match | 1 |
| FieldSetName | field (DataSet, `Tables[0]`, `Field<string>("Name")`) | bounded-match | sample |
| FieldMissingName | field (DataTable, `Field<string>("Missing")`) | mismatch | ArgumentException |
| FieldCaseOnly | field (DataTable, `Field<string>("name")`) | unknown | sample (runtime case fallback) |
| InternalFieldName | field (internal helper ReadShared in `Shared`, dbo.ReadItems) | bounded-match | sample |

The helper rows (Phase 4) use two private static helpers, `ReadTable(connection,
procedure)` and `ReadSet(connection, procedure)`, each scanned once as a Fill fact
whose call sites are bound separately through `sqlserver-helper-result-bindings/1`.
HelperTableName and HelperOtherProcedure call the same helper with different
procedures.

The Field rows (Phase 6) are compared through `sqlserver-field-read` with
`sqlserver-field-read-bindings/1`, one site each; the Field type argument is never
compared. `ReadShared(connection, procedure)` is an internal (`Friend`) static helper
in its own document (`Shared`), called once from `Cases`, so its call site is
enumerated across the compilation.
Seventeen reports per language, one selected row each: 10 match / 3 mismatch / 4
unknown; both languages 20 / 6 / 8. Runtime results never convert unknown into
static match. Exact code/binary and receipt hashes must be verified before joining
the two evidence layers. The existing 20 runtime checks remain; 34 linked method
checks (22 retained + 12 Field) bring the total to 54.
