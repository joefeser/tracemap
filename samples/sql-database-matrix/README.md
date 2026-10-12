# Public SQL/database matrix

Independently authored public synthetic libraries. Compile, inspect and scan only.
Never instantiate the fixture, call a fixture method, open a connection or execute SQL.
No private code/binaries/artifacts contributed to these fixtures.

Each target's OVERLOADS.md enumerates exact public declared API signatures and
assembly identities from metadata. Inherited APIs are represented by declaring
base types, not counted twice as provider operations. Matrix.cs/Matrix.vb contain
one separately named method per overload. Controls are authored separately with
CONTROL-EXPECTATIONS.json as a named, hand-authored oracle. The C# and VB compiler
may encode an overridden call on a base slot; original IL is independently checked
and remains distinct from source resolution. VB GetObjectValue helpers remain IL
observations and do not inflate the SQL case denominator.

References: Microsoft.NETFramework.ReferenceAssemblies 1.0.3 for seven classic
framework targets; System.Data.SqlClient 4.9.0 lib/net8.0 and Microsoft.Data.SqlClient
6.1.1 ref/net8.0 for net10.0; the latter's ref/net462 for net472/net48. Only the
net10.0 compatibility fixtures locally suppress CS0618/BC40000 for the intentionally
obsolete System.Data.SqlClient API; this does not recommend it for migration.
The tests pin both API behavior and the intentional fixture boundary.

See [the matrix and limits](../../docs/validation/SQL_DATABASE_MATRIX.md). Generate
source by passing explicit public DLL selections to SqlReferenceMatrix; it reads
metadata without loading assemblies. Never substitute application binaries.

```
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter 'FullyQualifiedName~SqlDatabaseMatrixTests|FullyQualifiedName~DatabaseCommandStateTests' -warnaserror
python3 scripts/validation/test_sql_matrix_expectations.py
python3 scripts/validation/sql-coverage-smoke.py --output /tmp/public-sql-matrix
```

The website-projectless/App_Code fixture proves Entry -> Wrapper -> ExecuteScalar
as a candidate route in the retained migration inventory. It is not runtime use,
a verified ASP.NET event, provider dispatch or whole-site completeness.

Modern fixture compiler/reference pin: SDK 10.0.302, Microsoft.NETCore.App.Ref
10.0.10. The native scan receipt retains exact generator and bounded input identity.

## Projectless source/report regression samples

`website-projectless/App_Code/QualifiedCommands.vb` adds a relative qualified
SqlClient receiver with a System.Data import, lifecycle-only calls, and an unrelated
same-named SqlCommand control. The qualified receiver is a Tier3 candidate, not
signed provider proof. Arbitrary namespace suffix matches are not accepted.

`wrapper-website` is a separate projectless VB website whose methods pass a
synthetic procedure name and dynamically constructed SQL to `wrapper-library`.
The library is independently buildable as net472, but no method may be executed.
`ProjectlessSqlReportingTests` tests three distinct inputs: application source
alone, application plus wrapper source (copied without a project file), and
application source plus the separately admitted wrapper DLL with IL enabled.
Missing wrapper source produces no database operation candidate and an explicit
report limitation, not evidence of no database use. Wrapper source exposes two
syntax operation candidates; argument propagation, actual procedure identity,
SQL text, parameterization and connection association remain unavailable. DLL
admission retains separate IL observations and does not bind application source
calls to that DLL automatically.

Run `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter
FullyQualifiedName~ProjectlessSqlReportingTests -warnaserror`. Reports display
candidate rule/tier/availability, include an empty-coverage warning, and disclose
the total/omitted count when the 50-row display cap is reached.
