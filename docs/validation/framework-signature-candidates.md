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
