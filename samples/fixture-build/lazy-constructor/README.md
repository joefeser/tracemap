# Property-based constructor logging corpus

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

- Mixed and compiled-only queries retain property-getter and constructor routes
  across the DLL boundary, including nested field initialization and logging.
- Same-name unrelated declarations are not joined.
- The logging endpoint retains command type Text (`1`), one caller substitution,
  a `call-result` origin, `unresolved-operand`, and explicit operand/virtual
  dispatch gaps. A literal supplied to the same ExecuteText method resolves.
- The identical literal returned by `LiteralText` currently remains unresolved.
  The regression checks its actual compiled instruction shape and literal
  equality, isolating missing return-value tracing from `BuildText`'s runtime
  string composition. This is a known limitation, not a successful resolution.
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
