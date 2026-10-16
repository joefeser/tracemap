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
compiler-produced MemberRefs with Mono.Cecil. The duplicate-candidate negative
control deliberately adds a second declaration fact after native extraction;
the four positive cases use only native extracted facts.

## Cases and evidence boundary

| Caller | Constructed target | Purpose |
| --- | --- | --- |
| ReadText | Container.Slot<string>.Current | Nested constructed reference type |
| ReadNumber | Container.Slot<int>.Current | Distinct value-type substitution |
| ReadOther | OtherContainer.Slot<string>.Current | Same member spelling, different declaring type |
| CopyText | Container.Slot<string>.Copy(ref string, string[]) | ByRef and array wrappers over type parameter |
| ReadPlain | PlainProvider.Current | Non-generic positive admission control |

The four native generic calls now have exact `compiled-il-call` edges to their
respective definition nodes under `combined.paths.constructed-generic-member.v1`.
The plain control still resolves. A duplicated matching declaration admits no
edge and retains an ambiguity gap with candidate count two. Inputs are unbound
compiled artifacts: successful resolution preserves `CompiledIlArtifactContext`
and does not claim authenticated source/build binding.

## Keep the production fix reviewable

The sample-only baseline is commit 89d8807b. The implementation replaces its
expected-gap assertions with exact expected edges, retaining metadata-shape
assertions and the non-generic control. Do not loosen them to accept either a
gap or an edge. The new parser is isolated from framework-version matching.

The resolver preserves complete assembly/type identity and definition-relative
parameter ordinals, validates closed declaring-type substitution through return
and parameter signatures, and requires one evidenced target. Equal concrete
arguments do not make different !n ordinals interchangeable. Graph nodes remain
generic definitions; no instantiated downstream body is invented.

Signature tests cover mismatched assemblies/versions, arity, ByRef/array wrappers,
type-parameter ordinals (including repeated arguments), nested owners and framed
delimiter-bearing names. Unsupported signatures and open/method type parameters
abstain. No claim of runtime dispatch, ECMA conformance, complete graph coverage
or recovered database chains follows from these cases. Full generic method
instantiation and specialization of downstream body calls remain out of scope.

## Validation

- Standalone caller/provider build: zero warnings and errors.
- The initial 127-fact CLI smoke had missing compiled inputs because its paths
  were relative to the sample scan root. Corrected smoke with absolute DLL paths:
  225 facts, `Level1SemanticAnalysisReduced`, with retained IL calls. The native
  graph tests always used absolute paths and exercised real compiled inputs.
- Focused tests include native unique/duplicate admission and parser boundaries;
  run the filter `FullyQualifiedName~ConstructedGeneric` for both test classes.
- Combined focused run with framework-signature candidates and two-hop controls:
  131 passed, zero failed or skipped. CLI build: zero warnings/errors.
- Full suite and language-adapter pinned smoke checks are deferred; no language
  extractor changed. This is reporting-layer resolution, not full semantic coverage.
