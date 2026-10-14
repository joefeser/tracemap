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
The full adapter CI installs this pinned SDK alongside the current SDK and checks
resolution from this directory before testing. A root-level build alone does not
validate the nested `global.json` used by semantic workspace scans.

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

## Compiled caller plus separate wrapper

`wrapper-build/SyntheticWebsite.vbproj` is a public net472 compilation harness for
the website source, referencing the separate SyntheticStore assembly. It is not an
ASP.NET publishing receipt or proof of production source/binary correspondence.
The procedure path delegates through two overloads before ExecuteNonQuery; the
text path carries a symbolic string composition to ExecuteScalar. Build only;
never invoke these methods or connect to a database.

`SqlWrapperRouteTests` verifies both complete routes, constant/hash and symbolic
argument states, grouped inventory, and caller-only/provider-only/source-only
missing-input controls. Existing compiled resolution supports this fixture; this
is new acceptance coverage, not a newly implemented generic resolver.

For an existing closed scan/index, `scripts/sql-wrapper-report.py` creates a
local-only selected-root SQL report. See
[the work-machine procedure](../../prompts/produce-sql-wrapper-inventory.md).
The helper retains failed/unmatched selections, links native path evidence,
records generator/component/input hashes, and preserves coverage warnings. It
does not extract readable SQL, enumerate all callers, or execute database calls.

```text
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~SqlWrapperRouteTests -warnaserror
python3 scripts/validation/test_sql_wrapper_report.py
```

For a retained index exceeding the default 512 MiB admission limit, explicitly
pass `--max-index-mib 1024` (maximum 16384 MiB). Optionally pass
`--query-timeout-seconds 600` (default 120, maximum 3600). These change admission
and wall-clock bounds, not path depth/count or graph work limits. Do not discard
assemblies or rescan merely to reduce file size. The local report records both.

`scripts/validation/sql-wrapper-large-index-smoke.py --cli <built-tracemap.dll>
--out <fresh-directory>` runs the public compiled fixture through a valid SQLite
index padded to 768 MiB. It tests default refusal, explicit admission and native
route retention. It is a file-size test, not a large logical-graph benchmark.
