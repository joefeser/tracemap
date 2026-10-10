# Property-based constructor logging corpus

## Repeat the operator workflow locally

From the repository root, in PowerShell 7:

```powershell
.\scripts\wlocal.ps1
```

No private configuration, saved shell variables, screenshots, SQL Server or
previous output is required. The command builds the real net48 website/provider
projects and runs the public regression corpus. It prints a fresh output folder;
use `-OutputRoot <new-folder>` to select one. Existing folders are refused, and
failed runs are retained. `-RequireWindowsPublish` additionally requires real
ASP.NET mapped and mapless publication on Windows; it refuses other platforms.

The `operator/` output contains these independently replayed layouts:

| Layout | Inputs | Expected selected-handler evidence |
| --- | --- | --- |
| `attached` | Website source plus website and provider DLLs | Dynamic Text scalar, literal StoredProcedure scalar, Fill |
| `separate` | Website source/website DLL scan, separate provider source/DLL scan, then combine | Same ordered getter/constructor/profile/lookup chain and three terminals |
| `separate-dll-only` | Same website, separate provider scan excluding VB source | The DLL evidence alone retains the three terminal routes |
| `reversed` | Separate inputs combined in provider-first order | Same ordered route and command assertions regardless of input order |
| `missing` | Website source/website DLL without provider | No invented database route; explicit gaps |

Each layout retains `site/`, optional `dll/`, `combined.sqlite`, and `report/`.
Positive layouts also retain `all/`, `fill/`, `capped/`, and `repeat/` path JSON.
They additionally render `cap-comparison.local.html` and
`unresolved-command.local.html` through the same diagnostic helpers used by
operators. The runner asserts one shared/two omitted exact sequences and one
unresolved command group, and runs all five saved-report helper test suites.
Assertions inspect actual method order, command type/text state, Fill-only
exclusion, a one-path cap with its gap, repeated paths/gaps, and unchanged index
bytes. `tests/deep-corpus.trx` records pass/fail. `validation.local.json` binds
the runner and bounded inputs and is written only after the required cases pass.

This exercises the public `scan -> combine -> report -> paths` command entry
points against actual on-disk indexes. It does not simulate native UI clicks.
The same corpus includes native start/package/resume tests and separate
mixed/compiled-only path tests; those are distinct coverage layers. No fixture
database method is executed. A green run is synthetic static regression proof,
not complete private application coverage or PR merge approval.

For a quick operator-only development run (without retained artifacts):

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~WebFormsOperatorWorkflowTests
```

## Fixture structure

### Command-return acceptance matrix

`LazyConstructorLoggingTests` exercises actual compiled VB provider methods through
both indexed and in-memory reporting. The local replay above includes these tests.

| Shape | Required outcome |
| --- | --- |
| Direct literal and literal-return producer | Same constant hash; return witness only for the producer case |
| Returned argument wrapper | Two retained return steps |
| Literal producer through two executor argument hops | Constant hash and two argument steps |
| `String.Concat` result through two executor argument hops | Unresolved call result; missing admitted target edge; no invented return step |
| Profile/getter/constructor lookup with dynamic concatenation | Scalar route retained; exact Concat producer asserted; unresolved return target distinguished from audit/Fill |
| Producer method declaration removed | Target-edge-missing gap, not a constant |
| Duplicate producer declaration | Upstream `CompiledIlTargetAmbiguous`; no target edge admitted to return projection, no arbitrary selection |
| Missing/duplicate return summary | Missing/ambiguous return-evidence gap |
| Changed provenance, malformed summary, excessive count/work | Typed refusal |
| Conflicting returned values, recursive producer, virtual producer | Unresolved with the corresponding disagreement/cycle/dispatch gap |

The legacy `IlCommandReturnTargetMissingOrAmbiguous` gap remains for compatibility.
Additional gaps distinguish `IlCommandReturnTargetEdgeMissing`,
`IlCommandReturnTargetEdgesAmbiguous`, `IlCommandReturnTargetMethodMissing`, and
`IlCommandReturnTargetMethodsAmbiguous`. Missing edges mean no admitted target in
this graph, not that the method does not exist. This corpus does not prove which
producer caused a private application's gap, and does not execute customer SQL
or claim to reconstruct dynamically composed SQL from hashes.

Public synthetic VB .NET Framework 4.8 source, compiled as two real DLLs. The
website and provider source directories contain no project files. These external
build harnesses are not an ASP.NET publication, and no database calls are run.

The pattern does not depend on session state. A shared backing field models lazy
property initialization; a session-backed getter is one possible real-world
implementation, not a requirement of this test.

The selected handler constructs `SyntheticChoices`. Its constructor evaluates
`LazyContext.EmployeeInfo.Region` as an argument to a separate provider lookup.
If the backing field is empty, the getter constructs `SyntheticEmployee`, whose
field initializer constructs `SyntheticPreferences`. Employee authorization and
exception branches, plus preference initialization, call the provider's logging
method. The provider is a separate DLL with this chain:

`InsertLog -> BuildText (returned string) -> ExecuteText -> ExecuteScalar`

`BuildText` is a producer of the argument, not a call-stack parent of
`ExecuteText`. The retained database route therefore does not by itself contain
the producer's return-value dataflow. This is the distinction under test.

## Assertions

The separate `Profile_Click` handler models a dynamic email lookup:
`EmployeeInfo getter -> ProfileEmployee constructor -> GetProfile -> GetEmail
-> ExecuteSql -> ExecuteScalar`. `GetEmail` concatenates a runtime argument
between two SQL literals. It creates a parameter collection but never supplies
it to the command. Its exception branch calls a distinct literal stored-procedure
audit endpoint. Tests pin the actual compiled `String.Concat` producer identity,
unresolved Text command, resolved StoredProcedure audit, and independent Fill
route in both mixed and compiled-only queries. This is synthetic static evidence,
not a claim about production execution or an exploitable trust boundary.

- Mixed and compiled-only queries retain property-getter and constructor routes
  across the DLL boundary, including nested field initialization and logging.
- Same-name unrelated declarations are not joined.
- The logging endpoint retains command type Text (`1`), one caller substitution,
  a returned symbolic Concat, and an explicit unresolved operand for the external
  Replace call, plus the virtual-dispatch gap. A literal supplied to the same
  ExecuteText method resolves.
- The identical literal returned by `LiteralText` resolves through separately
  retained producer-return evidence, including a returned-argument wrapper.
  The regression checks its actual compiled instruction shape and literal
  equality, distinguishing constant return-value tracing from `BuildText`'s
  runtime string composition, which remains symbolic rather than materialized. Missing, ambiguous,
  malformed or changed return facts, conflicting values, recursive producers
  and virtual return targets must remain explicit gaps.
- The getter batch covers two-getter literal and field-backed chains, returned
  argument wrappers, nested Concat, branch alternatives and a 16-frame limit.
  A separate cross-DLL handler feeds two getters through conditional argument
  normalization, Using/finally and the SQL wrapper. Field-backed instance/static
  returns stay unknown with the exact stopping method; no heap value is guessed.
- An independent business lookup reaches Fill with method-local constant text.
  Filtering to Fill excludes the ExecuteScalar routes; a zero unresolved count
  in that subset is not resolution of the logging command text.
- Static paths do not establish runtime branch feasibility, warm/cold execution,
  logging success, exception handling behavior, SQL parameter values or private
  application parity. The caught-exception call is retained statically, not
  claimed to execute. The cache has no concurrency guarantee.

Run from the repository root:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~LazyConstructorLoggingTests
```

The test builds the external harness, scans both DLLs, writes and combines the
index, and queries the selected compiled handler. It does not execute fixture
methods. The deep corpus validation entry point also includes these two cases.
