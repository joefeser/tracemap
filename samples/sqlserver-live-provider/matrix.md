# Authored static/runtime matrix

Author before the first scanner run. Both languages use Microsoft.Data.SqlClient
6.1.7 on .NET 10, and the same methods are scanned and executed. Runtime calls use
only the owned synthetic connection. Independent JSON declarations describe the
first result of dbo.ReadItems: Id, Name, Total. Runtime setup returns those three
columns followed by a second result with Value=99. No DDL completeness is claimed
for that multiple-SELECT body.

| Method | Route | Static expected | Runtime expected |
| --- | --- | --- | --- |
| ReaderName | reader | bounded-match | sample |
| TableName | DataTable | bounded-match | sample |
| SetName | DataSet | bounded-match | sample |
| MissingName | DataTable | mismatch | ArgumentException |
| CaseOnly | DataTable | unknown | sample (runtime case fallback) |
| LaterTable | DataSet | unknown | 99 (outside first-result scope) |

Six reports per language, one selected row each: 3 match / 1 mismatch / 2 unknown.
Runtime results never convert unknown into static match. Exact code/binary and
receipt hashes must be verified before joining the two evidence layers. The
existing 20 runtime checks remain; 12 linked method checks bring the total to 32.
