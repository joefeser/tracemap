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
