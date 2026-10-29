# Implementation state — 2026-10-29

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
