# Mixed desktop framework signature candidate matrix

## Scope

Independently constructed public synthetic tests for a 4.0 website caller and
2.0 legacy provider whose declaring assembly identities match but embedded
signature type scopes differ. A caller signature with `ArrayList` from mscorlib
4.0 and `DataSet` from System.Data 4.0 will not exactly equal the provider's 2.0
signature. The fix adds a **Tier3 candidate**, not an exact CLR binding.

No work-created implementation, private source, screenshots or binaries are
part of this fixture. The abstract report's ambiguity, scope, provenance,
dispatch and boundedness requirements inform the following independent cases.

## Executable use cases

| Family | Cases | Required result |
| --- | --- | --- |
| Exact controls | 2→2, 4→4 | Existing exact edge, no tolerant edge |
| Embedded versions | 4→2, 2→4 | One candidate path to ExecuteScalar; explicit runtime-binding gap |
| Website/App_Code/provider | Website 4.0 → App_Code 4.0 → provider 2.0 → Fill | Exact first DLL hop, candidate legacy hop, retained database terminal |
| Database endpoints | ExecuteScalar and DbDataAdapter.Fill | Both retain the candidate boundary and stop caller-value propagation |
| Exact priority | Exact target plus version-different target | Exact target wins |
| Candidate ambiguity | Two version-tolerant targets | No target edge; candidate count/gap |
| Exact ambiguity | Two exact targets plus version-different alternative | No tolerant rescue; exact ambiguity retained |
| Declaring identity | Wrong provider name/version | No route |
| Embedded identity | Wrong token/culture, parameter type, return type | No route |
| Provenance | Unbound, fixture-bound, unavailable | Candidate stays Tier3; unavailable excluded |
| Publish admission | Missing source receipt, wrong assembly hash | No source-root route |
| Dispatch | Virtual, nonvirtual callvirt, missing flags | Candidate remains; virtual/unknown flags retain separate dispatch uncertainty |
| Arguments | Caller string feeds provider command | Candidate route remains; argument propagation stops |
| Return values | Provider string result feeds caller command | No return-value binding through candidate |
| Traversal bounds | Depth 1, work 1 | Depth remains partial with existing prewalk witness; work exhaustion does not invent a path |
| Type grammar | Plain, vector array, byref, pointer, generic argument, nested type, modreq, modopt | Embedded scope comparison preserves structure |
| Framework allowlist | mscorlib, System, System.Data | Only neutral/b77a5c561934e089 desktop 2.0/4.0 pairs |
| Identity exclusions | Custom assembly, unknown version, namespace, type, count, return, name, arity, instance flag, delimiter-bearing name | Keys remain different |
| Parser refusal | Truncation, invalid length, trailing text, oversized input, excessive nesting, function pointer, shaped array | No tolerant key |
| Export | New edge and uncertainty through privacy projection | Public-safe categories retained, private markers absent, hashes remain projected-input-bound |

Every positive full-pipeline route also exercises mixed and compiled-only queries,
retained graph and HTML output,
except the dedicated return-flow boundary. Depth-limited graph mode retains its
own cutoff behavior and does not claim the deeper command trace was visited.

## Implementation boundaries

Rule: `combined.paths.framework-signature-candidate.v1`.

- Exact lookup precedes fallback; exact ambiguity is never bypassed.
- The declaring assembly remains exact. Only embedded allowlisted scopes are
  normalized, using a bounded parser that respects length-framed names.
- Original retained identities and provenance are not rewritten.
- Candidate uniqueness is across admitted indexed declarations, not proof of
  deployment selection, a binding redirect, type forwarding or runtime behavior.
- Unsupported signatures remain unresolved. This is not a universal
  assembly-version equivalence algorithm.
- Argument and return propagation stop at this boundary with named gaps.

## Commands and remaining boundary

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~FrameworkSignatureCandidateTests
pwsh -NoProfile -File scripts/tests/Test-WebFormsGraphShare.ps1
```

Authentic Windows ASP.NET 4.0 publishing against a separately compiled .NET 2.0
library remains a separate validation gate. Cecil-generated metadata validates
the scanner/report join and refusal behavior, not the native compiler, runtime
binding or the owner's private application. No private rerun is required to
reproduce the original missing-edge condition locally.

The controlled red check disables only the new MemberRef fallback: 2→2 and 4→4
still pass while 4→2 and 2→4 fail because the route collection is empty. Restoring
the fallback restores both mixed-version routes. This tests the actual missing
join rather than merely asserting that a normalization helper returns a value.

Local focused validation: 56/56 cases (25 pipeline scenarios and 31 signature
cases) pass with warnings treated as errors. The final adjacent regression batch
passed 318/318 cases with no skips in 3 minutes 12 seconds: the new matrix,
CombinedDependencyPathTests, LazyConstructorLoggingTests,
GroupedCompiledPathHandoffTests, CompiledBaselinePrewalkTests and
IlBodyEvidenceExtractorTests. Privacy export, graph refresh and wizard requery
script tests pass; the wizard script parses without errors. The CLI build has
zero warnings/errors and the modern-sample CLI smoke writes all required outputs
with 27 facts and Level1SemanticAnalysis. Full repository and authentic Windows
publishing results are not claimed by these targeted runs.

## Native validation feedback and inherited Fill control

The owner-reported Windows run of `473cc541` built native CLR4 website/App_Code
and a CLR2 provider, but C# and VB callers preserved the provider's embedded 2.0
signature. Both DLL hops were exact: that run did **not** reproduce the version
candidate mismatch. Its zero routes were instead traced to an encoded
`System.Data.Common.DataAdapter.Fill` call missing from the reporting terminal
matcher. This is a distinct defect; it must not be explained as version binding.

Seven additional synthetic cases pin base-class Fill under 2.0 and 4.0, a CLR4
caller preserving a CLR2 provider signature with no candidate edge, and wrong
namespace/assembly/token/culture refusals. Before the terminal fix all three positive cases
produce zero routes and the initial namespace/assembly refusals pass. The command extractor already
recognized DataAdapter; the fix aligns reporting with that retained evidence.

The existing `DbDataAdapter.Fill` surface is a historical Fill-family selector,
now including the authentic base-class encoding. Original call facts and retained
graph calls keep `DataAdapter` unchanged; the surface limitation explains this
grouping. Both mixed and compiled-only queries and the existing Fill-only
selector are tested. API reference:
[Microsoft DataAdapter.Fill documentation](https://learn.microsoft.com/en-us/dotnet/api/system.data.common.dataadapter.fill?view=netframework-4.8.1).

The Windows fixtures should be re-queried with the new binary without rebuilding
them, changing their emitted signatures, or introducing an expected candidate.
The original private case remains unavailable and unverified by that native run.

Follow-up local validation: all 183 selected tests pass with no skips and warnings
treated as errors, including all 63 framework/boundary cases plus
CombinedDependencyPathTests, GroupedCompiledPathHandoffTests and
CompiledBaselinePrewalkTests. See the
[Windows unchanged-fixture requery prompt](../WINDOWS_BASE_FILL_REQUERY_PROMPT.md).

### Bidirectional inherited-terminal matrix

Five further synthetic metadata controls complete the 2/4 cross-product for
inherited `DataAdapter.Fill`: differing embedded signatures in both directions
must retain a Tier3 candidate and stop argument propagation; provider-preserving
signatures under all four caller/provider pairs must remain exact. All exercise
normal, compiled-only, Fill-only, retained graph and HTML reporting. The reverse
2-to-4 control does not claim a native CLR2 compiler or runtime can load a CLR4
provider. It checks only how the indexer handles those independently encoded
identities. Native validation remains a separate gate.

Validation of this extension: 188/188 selected tests pass, zero skips, warnings
treated as errors; this includes all 68 framework/signature cases and the same
three adjacent reporting suites named above. The extension changes tests and
documentation only, not production extraction or reporting behavior.

### Unchanged native requery result

Windows validation at `ae9b439f591444bdee51a13a0705348454e445b4` passed
63 focused tests and a warnings-as-errors CLI build. CLI SHA-256:
`921244e7824b540a4bc83d2cd5a903360473e6e9a53afeff44de0f619095c9ad`.
Both native C# and VB retained indexes produced one path, one grouped chain and
one variant in each of normal, compiled-only and Fill-only modes. Two exact DLL
hops and original 2.0/4.0 signature scopes remained unchanged, with zero version
candidate edges and no traversal truncation. All 48 original fixture/input files
were verified unchanged. Reduced coverage and unbound source provenance remain;
these results are not a private-application or runtime validation.

### Private acceptance and remaining traversal diagnosis

The owner subsequently supplied a sanitized private-application validation
summary: the original selected handler retained 28 grouped chains and 116
variants, including Fill and ExecuteScalar, using the unchanged retained index.
The summary reported exact DLL hops, no framework-version candidate edges and
visible HTML endpoint explanations. This is owner-supplied acceptance evidence,
not an independently inspected private artifact or complete-coverage claim.

The result remained truncated. The reported work-limit diagnosis has not been
independently established from its retained cutoff reasons. The PowerShell
summary now prints the measured work and configured budget when retained, plus
explicit work/depth/cycle/path/frontier gap counts from the receipt-hash-checked
handoff. It never infers work exhaustion from truncation alone. Synthetic script
tests cover all five reasons and unknown historical measurements. No traversal
algorithm, evidence admission or safety limit was changed without a reproducer.

The follow-up owner-supplied diagnosis verified the handoff against its receipt:
10,535 measured work units out of a configured 2,000,000, with zero work/path/
frontier cutoffs. Its 248 retained gaps consist of 225 bounded terminal-route
uncertainties and 23 immediate publish-member identity round trips. This corrects
the earlier work-exhaustion interpretation; increasing the work budget would not
address these recorded causes. Counts are gap records, not counts of missing paths.

### Depth and identity regression build-out

Six public synthetic pipeline cases exercise depths 2, 4 and 8, each with ordinary
enumeration and distinct-terminal inventory. The graph contains two separately
evidenced short routes, a deeper terminal and an unresolved leaf. Assertions pin:

- both short evidence variants survive at every depth;
- ordinary enumeration admits the deep route only when its depth permits it;
- terminal inventory retains a deeper witness separately from route completeness;
- an unresolved leaf never becomes a terminal route;
- work remains bounded, without falsely reporting a work cutoff;
- repeated queries reproduce paths, gaps and work counts deterministically.

The existing real-scanner synthetic publication-receipt fixture additionally
asserts that both directions of the publish-member identity link remain in the
retained graph, the immediate return is labeled as a candidate identity cycle
(not application recursion), terminal paths still exist, and no retained path
repeats a node. Its four invalid-receipt controls remain closed.

These cases pass against the existing traversal implementation. They reproduce
the reported categories but do not demonstrate a traversal defect or prove that
every private branch is covered. No algorithm change or warning suppression is
justified by these results. Private source/assemblies are not part of the tests.

Validation at `f036a7237c4772a83eac9a339fb1047b94b1f1ad`: local selected
regressions passed 199/199 (zero skips). Windows focused depth/publication/
roundtrip tests passed 14/14, and the wizard requery script passed. The initial
Windows test command exited 1 despite passing tests because MSBuild reported
MSB3491 for a generated assembly-attributes file. One retry with `-m:1
/p:BuildInParallel=false` completed with exit 0; the underlying build-error cause
is not established. Native and private artifacts were untouched. No full-suite,
application-execution or SQL-execution claim is made.

### Explicit deeper retained queries

The handler-requery CLI now accepts `--max-depth 1..64`, exposed by
`wrequery.ps1 -MaxDepth`. Omission retains the original plan's depth; all other
budgets remain unchanged. The effective depth is in the new receipt's existing
hash-bound query and the original manifest/checkpoints are not edited. Completed
and recovery contexts are tested; invalid/duplicate values are refused before
output creation. PowerShell inspection-only mode rejects a depth override.

Local validation: 201 selected tests pass with zero skips, plus the wizard
PowerShell script tests. Depth hash mutation, depths 1/32/48/64, compiled-only and
graph-mode combinations, and byte-for-byte original artifact preservation are
covered. The deeper synthetic route tests distinguish route enumeration from
terminal inventory; private additional chain counts remain unmeasured.
Use [the staged work-Opus prompt](../CLAUDE_DEEPER_HANDLER_REQUERY_PROMPT.md) for
fresh private depth-32 and depth-48 queries without rescanning.

Windows validation at `cec30f14fb782e2c714b375662cbc3e4db6cf147` passed:
sequential CLI build with zero warnings/errors, 13/13 focused depth/recovery
tests with zero skips, and the wizard PowerShell script after tests completed.
Existing fixtures and outputs were untouched. This validates the override
workflow, not additional private chains; the staged private requery is next.

That run also exposed a presentation omission: endpoint limitations were retained
in JSON but absent from grouped HTML. The writer now displays HTML-escaped
endpoint limitations beside each database endpoint. A new HTML assertion failed
for all four preserved-signature controls before the change; the fix does not
alter graph construction, terminal matching or evidence tiers.

Final Windows validation of `89aa6763cb58a99d524d39c51d160f39c3589030`:
68/68 focused cases pass with zero skips; CLI build has zero warnings/errors.
CLI SHA-256: `cfc031c630fab11a45651114cd19bf71fe21ff637a4f478bdc70149ac0329285`.
All six native query combinations retain the same 1/1/1 path/chain/variant
counts, two exact DLL hops and zero framework-version candidates. The grouping
explanation is now visible in HTML while supporting IL retains DataAdapter.Fill.
The final preservation check covered 479 existing files, including prior outputs;
all were unchanged. Coverage/provenance gaps remain and the private application
is still unvalidated. No application or SQL execution was performed.

## Projectless helper passing modes

The public `vb-unbridged-helper` fixture independently tests a source helper with
a `ByRef Integer` parameter and retained compiled calls through a separate
provider to `SqlCommand.ExecuteScalar`. It never executes that method or SQL.

Syntax extraction now retains ordered `parameterPassingModes`; the bounded
report projection preserves them. Publish-member matching requires a matching
ByRef/ByVal mode before comparing the element type. Receipt hashes, qualified
type/name matching, unique-candidate admission and Tier3 labeling are unchanged.
Wrong, malformed, inconsistent or absent ByRef mode evidence is refused.

The matrix separately proves that an already-retained compiled Start-to-helper
route survives when the source helper cannot be bridged. Missing source bridges
must not be described as missing IL calls. Older indexes need source
re-extraction to gain passing-mode evidence; merely requerying them cannot add it.
This synthetic defect is not proof of the cause of the private application's
remaining source gaps. Alias imports and identifier-side array declarations
remain separate unsupported-shape investigations, not guessed matches.

Focused local validation: 251 tests passed (framework candidate, VB extraction,
publish-member index, combined paths, grouped handoff and baseline prewalk
suites), including eight helper cases. CLI build completed with zero warnings
and errors. Authentic Windows compilation of this new helper case remains a
separate validation gate.

Windows validation of `b058b3c1e2466afc4cc4f9a83e985c489af30158`: serial CLI
build passed with zero warnings/errors. The requested framework candidate, VB
extraction and publish-member index classes returned 124 passes and one failure,
zero skips, in 3m29s. The failure was
`VisualBasicExtractionTests.Vb_scans_never_promote_event_wiring_to_resolved_edges`;
the quiet output did not retain assertion details. Its cause is not established.
No retry or broad suite was run. New native helper compilation was deferred;
existing native artifacts were preserved. This is partial Windows validation,
not a claim that all tests or private application routes are fixed.

## Nested source-to-metadata type boundaries

A separate synthetic fixture, `vb-nested-publish`, reproduces a nested VB class
whose flattened dotted name was previously interpreted as a namespace plus one
type. The reporter now uses the declaration's retained `lexicalNamespace` to
separate the namespace from the nested names and encodes each type name using
the same length framing as the metadata identity. The index reads all framed
nested names; it does not flatten them into a namespace. Existing indexes with
that namespace evidence can be requeried without source re-extraction.

Before the change, both CLR2/CLR4 nested-source roots failed to reach the database
terminal, and a namespace-lookalike decoy incorrectly produced a candidate path.
The regression matrix requires the real nested route, rejects that decoy,
preserves duplicate-candidate ambiguity, and refuses to invent nesting when
namespace evidence is missing. Receipt/hash, parameter-shape and Tier3 boundaries
remain unchanged. This is synthetic metadata evidence, not native execution.

Member/assembly bridge gaps now retain the exact source declaration node ID so
the read-only classifier can associate them with the same retained cutoff node.
No declaration-to-callsite inference or causal/absence claim is added. Old
handoffs are not modified; they need report regeneration to gain these node IDs.
Missing import scopes, unsupported generic parameter syntax and the private
route count remain separate unresolved questions.

Local validation for this change: 230 focused tests passed with zero skips
(framework signature candidates, publish-member index, combined dependency
paths, grouped handoff and baseline prewalk). The CLI build had zero warnings
and errors. The earlier Windows event-wiring failure is not claimed resolved
by this reporting-only change; its test class was not rerun locally here.

Windows validation of `e0cf7889614689a73b57c7ef4c9a2cca640e63be`: the framework
candidate and publish-member index classes passed 104/104 with zero failures or
skips, confirmed from retained TRX. Serial CLI build had zero warnings/errors;
CLI SHA-256 was `c30c039e8c85b836c9700ab00755becdf6d43d0e5edbd2e8a0cf87fc96c86a80`.
The command completed in 2m06s. New native fixture compilation remains deferred;
existing native artifacts were untouched. No private application or SQL ran.

## Generic source parameter matching

The independent `vb-generic-publish` fixture adds a source method parameter of
`Dictionary(Of String, Integer)` with matching compiled metadata and a retained
database-terminal route. CLR2 and CLR4 cases cover the actual scan/index/report
pipeline. Closed generic definitions, arity and ordered arguments are compared
recursively using the existing namespace admission rules. Nested closed generics
and vector arguments are covered. Method signature splitting now distinguishes
commas inside generic arguments from commas between method parameters.

Wrong argument types, missing import scope, malformed metadata, wrong arity and
excess nesting fail closed. No default ASP.NET imports or namespace guesses are
added. Method variables such as `List(Of T)` require the newly retained ordered
`methodTypeParameters` field and matching metadata method arity/ordinal. The
`vb-generic-method-publish` fixture tests two variables deliberately used in
reversed order, including wrong-ordinal, duplicate-name and missing-field refusals.
Containing-type variables (`!0`) remain unsupported. This is not a
claim to fix all parameter-shape gaps reported by the private application.
Existing retained parameterTypes/importedNamespaces can be requeried; no new
source field is required for these closed generic shapes.
Older indexes require targeted source re-extraction for method-generic variable
evidence; requery alone cannot manufacture the names or ordinal mapping.
Duplicate retained source identities in one file fail closed before member
bridging, including collisions between generic overload declarations. The source
identity schema is not rewritten by this change.

The additive method generic evidence is emitted by `vb-syntax/0.3.24`. Local
focused validation passed 309 tests, including VB extraction, generic positive
and refusal cases, publish-member indexing, combined paths and handoff/prewalk
regressions. This does not resolve the earlier Windows event-wiring failure or
establish additional private routes without the owner-side comparison.
After the extractor-version update, the 111 generic/framework candidate tests
were rerun successfully; the CLI build completed with zero warnings/errors.
The keyword/escaped-identifier follow-up additionally verifies that a method
variable named `[Integer]` cannot capture a predefined `Integer` type, and that
`!!0` never substitutes for containing-type `!0`. All 112 generic/framework
candidate tests passed after that follow-up.

Windows validation of `c8984c35f4c950bb8677b9a677eb4d3300c86f31` passed 134/134
generic, framework candidate and publish-member index tests, with zero skips,
in 2m18s. Serial CLI build had zero warnings/errors; its SHA-256 was
`fe272a4fdca77ccbc58f42ae46aacdcc4e1a33e68f7bc67fdd239b2c7b4bd838`.
TRX counts were verified. Native new-case compilation was deferred, existing
native artifacts remained unchanged, and no private application or SQL ran.
Windows also validated the keyword follow-up
`8166c4d2b45f4400bdf0c0bd6e1763b09a7fe666`: serial CLI build was clean and all
21 PublishGenericParameterTests passed with zero failures/skips, including the
predefined-type regression. No larger suite or native execution was repeated.
## Explicit configured VB imports (2026-10-11)

`vb-syntax/0.3.25` retains an explicit subset of ASP.NET VB compilation imports
separately from source Imports. The scan-root Web.config must be inventoried and
must explicitly identify the framework VBCodeProvider in System. App_Code method
declarations retain namespace names, config path, exact config-byte SHA-256, kind
and limitation. Fact identities include these properties. Bounded reporting
retains them and uses them only when kind/path/hash evidence is present.

Microsoft's [AssemblyBuilder reference source](https://github.com/microsoft/referencesource/blob/main/System.Web/Compilation/AssemblyBuilder.cs)
shows application pages/namespaces becoming global VB /imports through
AddVBGlobalNamespaceImports, including App_Code compilation. Local clear/remove
operations apply to the retained explicit collection; explicit compilerOptions
imports are then added. No inherited default namespaces are fabricated.

The scope is deliberately partial: no external config, location overrides,
nested App_Code config, source/config aliases, arbitrary providers, legacy
compilation/compilers overrides, or MSBuild project evaluation. A missing root
provider declaration is not proof of the default provider. These boundaries
withhold enrichment; existing analysis/bridge gaps remain. Codebehind files are
not enriched by this App_Code-only implementation. Old indexes require source
and configuration re-extraction; changing the reader alone cannot add evidence.

Focused tests include collections, event/control signatures, CLR2/CLR4 synthetic
PEs, conflicting imported overloads, missing provenance, collection clearing and
removal, malformed/oversized XML and unsupported configuration. These are public
synthetic tests, not a new private route-count claim. Native compilation and the
private 57-chain/238-variant comparison remain separate acceptance checks.

Local focused regression: 291 passed, zero failures/skips (14 seconds). CLI
build: zero warnings/errors. No private inputs or application/SQL execution.
The VB extraction smoke slice also passed 33/33. A real CLI scan of the public
fixture completed with 31 facts at Level3SyntaxAnalysis and retained separate
source/configured imports and the configuration hash. The follow-up aggregate
import-count boundary passed 24/24 configured-import tests; explicit VB auto-import
counts toward the same 128-entry cap. Native compilation remains deferred.
