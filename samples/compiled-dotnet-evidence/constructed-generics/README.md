# Constructed generic member resolution sample

Public, hand-written VB provider and C# caller. No private application code,
framework DLL, configuration, SQL, credentials or extracted artifacts are used.
Both projects are libraries; tests read their metadata and IL without executing
the sample. This is a modern .NET compiler fixture, not a historical ASP.NET or
VB `My` compiler-generation reproduction.

Build from the repository root:

```sh
dotnet build samples/compiled-dotnet-evidence/constructed-generics/caller/ConstructedGenerics.Caller.csproj
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~ConstructedGenericSampleTests
```

The test project's build-only reference builds both projects in its selected
configuration. Generated binaries remain ignored. The tests run the real scanner,
write its normal index, combine it and inspect graph admission. They also inspect
compiler-produced MemberRefs with Mono.Cecil; no synthetic facts are substituted.

## Cases and evidence boundary

| Caller | Constructed target | Purpose |
| --- | --- | --- |
| ReadText | Container.Slot<string>.Current | Nested constructed reference type |
| ReadNumber | Container.Slot<int>.Current | Distinct value-type substitution |
| ReadOther | OtherContainer.Slot<string>.Current | Same member spelling, different declaring type |
| CopyText | Container.Slot<string>.Copy(ref string, string[]) | ByRef and array wrappers over type parameter |
| ReadPlain | PlainProvider.Current | Non-generic positive admission control |

At the characterization baseline, provider generic definitions and bodies are
retained, and caller MemberRefs carry constructed declaring types. The generic
calls have no `compiled-il-call` edge and have callsite-correlated
`target-assembly-present-without-bound-provenance` gaps under
`combined.paths.compiled-il-bridge.v1`; the plain call has an edge. Inputs are
unbound compiled artifacts, not authenticated source/build bindings. A generic
gap is not evidence of a missing DLL, ambiguous dispatch or a database route.

## Keep the production fix reviewable

This sample commit changes no production resolver behavior. The tests explicitly
characterize today's limitation; they are not a requirement to preserve it.
In the implementation commit, replace gap assertions with exact expected edges,
while retaining metadata-shape assertions and the non-generic control. Do not
delete the cases, skip them, or loosen them to accept either a gap or an edge.

The future fix must preserve complete assembly/type identity and generic
parameter ownership, substitute declaring-type arguments through signatures,
and require one evidenced target. Do not collapse different constructed types
or methods by short names or strip generic arity/ByRef/array markers.
Keep artifact provenance warnings distinct from target-resolution failures.

Before claiming broad generic support, add negative tests for duplicate candidate
provenance, missing definitions/bodies, wrong arity, nested generic outer types,
method generic parameters versus type parameters, and unsupported signatures.
Those cases are not covered by these four characterization cases. No claim of
runtime dispatch, ECMA conformance, complete graph coverage or recovered database
chains follows from this sample.

## Validation of the sample-only change

- Standalone caller/provider build: zero warnings and errors.
- Focused characterization suite: 4 passed, zero failed or skipped.
- CLI scan with both compiled inputs and `--il-body-evidence`: completed,
  127 facts, `Level1SemanticAnalysisReduced`. This is explicitly reduced analysis,
  not a clean semantic-coverage claim; the graph tests check the compiled facts.
- No production resolver or rule behavior changed. Full suite and language
  adapter pinned smoke checks were not run for this fixture-only change.
