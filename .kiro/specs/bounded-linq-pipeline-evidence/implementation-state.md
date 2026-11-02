# Implementation state — 2026-10-29

Current: source extractor 0.2.0; compiled extractor 0.2.21/policy v22 (typed-scalar
copy fix only, no delegate interpretation). Focused LINQ+IL SQL 179/179 passed;
refreshed C#/VB source CLI smokes each 6 candidates/7 intentional gaps. Full Debug
verification passed: 5633 passed, 3 skipped, 0 failed. Earlier compile-only/deferred notes below are historical
and superseded by the executed-validation sections.

Branch: codex/sql-compiled-wrapper-evidence. Base: 9ca865bc8 (exact lifecycle attribution diagnostic implementation).

This is a separate SOURCE-SEMANTIC evidence feature, not a compiled lifecycle
interpreter bypass. It handles two independently authored expression shapes in
C# and Visual Basic. Existing source SQL consumer refusals, Fill association,
compiled call dispatch, ArrayList/execution identity, root selection, schemas and
all three existing boundary flags remain unchanged. No LINQ callback is executed.

Rule dotnet.semantic.linq-pipeline.v1 emits LinqPipelineCandidate (Tier1 compiler
binding evidence), or its own Tier4 AnalysisGap for a recognized but unsupported
shape. Schema linq-pipeline/1; extractor linq-pipeline-evidence/0.1.0. Both semantic
extractors call the shared implementation. Missing semantic binding has no textual
fallback that can fabricate pipeline facts.

Supported source shapes:
- An object collection reference -> GroupBy(identity key) -> Select(anonymous
  object with grouping.Key and grouping.Count()) -> Where(projected count equals
  an external Int32 local/parameter/field), optionally ToList. Records captured
  symbol hash, not its value. Custom equality/comparer behavior remains unproven.
- DataTable symbol reference or DataSet.Tables[constant ordinal0..15] -> exact
  DataTableExtensions.AsEnumerable -> Enumerable or EnumerableRowCollectionExtensions
  Select(one DataRow lambda with parameterless typed object initializer) -> ToList.
  Each assigned property/field must derive from a constant named DataRow indexer.
  Observed Object/String.ToString, String.Trim, Int32.Parse(string) and direct
  Object/String/Int32 conversions are retained as transforms. Conversion/null/value
  success, user setters/constructors and callback effects are not inferred.

No table/row existence, cardinality, enumeration, purity, SQL execution or Fill
provenance is claimed. These candidates do not feed existing result-schema
comparisons or clear lifecycle ldftn refusals. Separate source symbol references
are not runtime allocation identity. Raw column/member/capture names and source
snippets are not stored: dependency identifiers and column names are hashed.
Framework declaration assembly/type/method identities are retained as metadata.

Framework profiles require exact names, public-key tokens, neutral versions and
supported signatures: legacy mscorlib/System.Data2.0.0.0 or4.0.0.0;
System.Core/DataSetExtensions3.5.0.0 or4.0.0.0; modern System.Linq/Data.Common/
System.Runtime and System.Private.CoreLib major4..10 with0.0.0 minor/build/revision
and the appropriate b03f or7cec token. Identity is declaration evidence, not
framework binary authentication. No method-name-only classification.

Bounds:20,000 syntax nodes/document,128 candidate sites/document,512 operations/
expression,128 operations/callback,16 mapped members. Callback loops/conditions/
handlers/await/throws and unsupported calls fail closed. Comparer overloads,
indexed selectors, alternate group keys, dynamic columns, custom conversions,
unresolved table sources, nonliteral table ordinals, queryable providers and
local-alias-spanning pipeline reconstruction are outside the subset. Grouping is
not mislabeled as a DataTable operation. Dynamic DataTable.Select strings and
CopyToDataTable are not admitted or evaluated.

Independent public C#/VB fixtures live in the existing end-to-end sample projects.
Tests define object grouping, direct table and dataset projections, captured count,
negative overload/helper/column controls, unsigned framework-name impostor and
replay/contract separation checks. Tests/scans were NOT executed, per Joe.
Compile-only checks use --no-restore -c Debug -warnaserror for both sample projects
and the test project (which also builds CLI/Core). No private source or photo/OCR
artifact was added to Git. No PR, merge, ACK change or private acceptance claim.

Remaining: run authored tests when authorized, then broader shared-core gates;
verify native semantic extraction separately. The earlier compiled attribution
investigation remains active and independent of this source feature.

Final compile-only results: both net48 end-to-end fixture projects and `dotnet build src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore -c Debug -warnaserror` passed with zero warnings/errors, including CLI/Core. Earlier compilation errors in a test reference selector and a local variable name were corrected before the final successful build. `git diff --check` passed. No tests, source scans or private/native scans ran.

## JToken pipelines and first executed validation — 2026-10-29

Extractor linq-pipeline-evidence/0.2.0 adds the source-semantic shape:
IEnumerable<JToken> or JToken symbol reference -> Where(token[string-key] converted
to String equals literal) -> Select(token[string-key].Value<String>()) ->
String.Join(literal separator, sequence). Direct and single local pipeline aliases
are supported. Alias declarations must precede the join, with no assignment or
ByRef escape anywhere in the bounded containing operation tree (4096 operations).
All existing expression/callback/document limits remain. Keys and the predicate
literal are hashed; source values, paths and snippets are not retained.

The JSON declaration profile is exactly Newtonsoft.Json assembly13.0.0.0, neutral,
public key token30ad4fe6b2a6aeed; synthetic projects pin NuGet13.0.3. The test package
already references13.0.3 transitively. No new Newtonsoft dependency is added to the
Core scanner. String.Value uses the exact inspected signed Extensions.Value<T>
overload; JToken-to-String conversion requires the signed op_Explicit declaration.
JToken.Item must have one Object argument containing a constant string. Arbitrary
helpers, dynamic keys/values, reassignment/ref escape, alternate value types and
unsupported signatures stay gaps. Input JSON/deserialization/JSON-path origin,
callback/conversion effects, null/value success and enumeration remain unmodeled.
A null-guarded synthetic JSON-path example intentionally records only the observed
local sequence symbol dependency, not reconstructed JSON lineage or reachability.
No compiled lifecycle delegate admission or dynamic DataTable.Select support.

The first actual focused execution found bugs the prior compile-only checks did
not expose: C# indexer symbols require MetadataName Item, VB single-line functions
include an implicit return of IsFunctionValue local, and VB reduced Select has
fewer visible type arguments. The fixes inspect compiler metadata/operation kinds;
they do not replace unknown evidence. Implicit user-defined conversions are now
preserved rather than stripped before their operator identity is checked.

Focused command:
`dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore -c Debug -warnaserror --filter FullyQualifiedName~LinqPipelineEvidenceTests`
Result:7 passed, 0 failed, 0 skipped. Includes C#/VB grouping/row mapping/JToken,
negative controls, deterministic replay and unsigned framework-name impostor.

Public native source scans (no application/fixture methods executed):
`dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/sql-compiled-wrapper-evidence/csharp/end-to-end --out /tmp/tracemap-json-linq-native-csharp-20261029-a1`
and the corresponding vb project/output. Both completed, all five required scan
outputs present. Each emitted6 LinqPipelineCandidate facts (1 object grouping,
2 DataTable projections,3 JToken chains) and 7 intentional rule gaps. Whole-scan
coverage remained Level1SemanticAnalysisReduced; this is not complete-project or
private acceptance. Total facts: C#3498,VB2060. These are local working-tree smoke
outputs, not frozen acceptance receipts. Full Debug -warnaserror subsequently passed; see final verification below.
Pinned OSS source adapter smoke reruns are explicitly deferred for this local
bounded-feature iteration; public C#/VB native source smoke coverage is recorded
above, not relabeled as a pinned OSS pass.

## Executed compiled controls exposed a typed-scalar copy gap

The first full Debug pass (now explicitly authorized by Joe) reached two failures
in the previously unexecuted VB stack-name positives: lifecycle-object-copy-unresolved.
That pass was stopped after the concrete failure to repair it before restarting.
Exact signed RuntimeHelpers.GetObjectValue now preserves the SAME unknown input
origin only when its declared type is one of the existing immutable scalar types.
Untyped Object inputs and tracked command/parameter/DataSet references still refuse;
no custom mutable struct, value assumption or object-identity substitution is added.
Public C#/VB ScalarCopyProduce, UntypedCopyRefusal and TrackedCopyRefusal controls
verify both sides. This intentional compiled change bumps IL extractor 0.2.21 and
policy v22; external trace policy 3 is unchanged. Source LINQ remains a separate rule.

Executed focused commands after the fix:
- LinqPipelineEvidenceTests + IlSqlTraceStackNameTests + IlSqlTraceArgumentTests:
  24 passed, 0 failed, 0 skipped.
- LinqPipelineEvidenceTests + all IlSql test classes:
  179 passed, 0 failed, 0 skipped (23seconds).
Both used `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore -c Debug -warnaserror --filter ...`.
The restarted full Debug solution suite subsequently passed after space correction; see final verification below. Earlier full-run output is a failed/canceled iteration, not a pass.

Refreshed CLI source smokes after the scalar-copy fix used fresh a2 output directories with the same commands recorded above. Both completed with all required outputs, six LINQ candidates and seven intentional gaps per language; C# total3564 facts, VB2096. These remain working-tree, reduced-coverage public synthetic checks, not private acceptance receipts.

## Final local verification — 2026-10-29

Current code versions: source LINQ 0.2.0, compiled IL 0.2.21 / evidence policy v22;
external trace policy 3. Joe explicitly authorized local verification, superseding
the earlier test deferral for this run. No private inputs or acceptance run.

- Focused source LINQ:7 passed, 0 failed, 0 skipped.
- Source LINQ plus trace argument/stack-name controls after scalar-copy fix:
  24 passed, 0 failed, 0 skipped.
- Affected source LINQ plus all IlSql test classes:179 passed, 0 failed, 0 skipped.
- Full Debug solution gate:5633 passed, 3 skipped, 0 failed, exit 0,45.7511minutes.
  Command: `dotnet test src/dotnet/TraceMap.sln --no-restore -c Debug -warnaserror --logger 'console;verbosity=normal'`.
  Local log: /tmp/tracemap-linq-full-debug-after-space.log.
- C#/VB source CLI smokes: each 6 LINQ candidates (1 grouping,2 DataTable,3 JToken)
  and 7 intentional rule gaps; all five required outputs present. Whole scans
  retained Level1SemanticAnalysisReduced, not complete-project acceptance.
- `git diff --check` passed. Builds used -warnaserror; zero compiler/analyzer warnings.

The first broad attempt exposed the old VB typed-input value-copy refusal; fixed
and pinned by positive/untyped/tracked-reference controls. A later broad attempt
was canceled after SQLite/IO storage-exhaustion failures; Joe corrected space and
the full gate above then completed successfully. Failed/canceled runs are not
counted as passes. Pinned OSS smoke reruns remain explicitly deferred; local public
C#/VB native smokes and the full suite do not get relabeled as that separate gate.
Windows/platform skips are retained; this is not private Windows acceptance.

Compiled delegate/LINQ interpretation, dynamic DataTable.Select, private root
attribution/lineage and owner-schema acceptance remain outside the proven result.
No field bypass, SQL route weakening, application deployment, PR or ACK change.

Full-suite skips retained by the existing tests (not counted as passes):
- HttpReportProvenanceInteropTests.Native_http_receipt_verifies_without_claiming_path_presence
- OracleGoldenLaneTests.Full_generated_golden_sweep
- IlCommandBindingExtractorTests.Deep_projectless_Windows_ASPNET_publish_preserves_exact_chain_and_command_binding
The opt-in full generated golden sweep was not executed.
