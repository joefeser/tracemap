# Public CLR method-signature matrix

Tracking: #767, under #759. This is a bounded public fixture slice, not complete
language equivalence or completion of either issue.

## Comparison contract

All three fixture assemblies declare
`TraceMap.CompiledFixtures.Equivalence.SharedShape`. The common name is a
collision sentinel, not an identity join. The tests compare only the complete
`signature` property of `dotnet.compiled.member.v1` method facts after the
Mono.Cecil and System.Reflection.Metadata readers agree. Each signature has a
hand-authored golden, including calling convention, instance flags, method
generic arity, return type and parameter types. No production formatter is
used to manufacture the expected strings.

Every matching triplet must retain three distinct assembly identities,
member endpoint identities and fact IDs. Assertions retain scan commit,
extractor ID/version, metadata token/location, the non-source `1..1` span,
rule, tier, limitation, exact extractor DLL SHA-256 and bounded-input SHA-256.
Inputs are deliberately unbound: scan commit is not binary-source provenance.
No source/PDB/IL/rewrite relationship is inferred. F# source extraction remains
unsupported. Signatures do not capture complete member semantics (for example,
attributes, optional defaults or generic constraints), and do not prove runtime
behavior, interchangeable APIs or cross-build identity.

## Executable cases

Sources are the `FixtureShapes.cs`, `FixtureShapes.vb`, and `FixtureShapes.fs`
files under `samples/compiled-dotnet-evidence/`. All methods below are static;
C# `static`, VB `Shared`, and F# `static member` share that CLR shape.

| Case | Source construct in C# / VB / F# | Expected signature shape | Counterexample / non-claim |
| --- | --- | --- | --- |
| CLR-SIG-001 | `Select(int)` / `Select(Integer)` / `Select(int)` | `(Int32) -> Int32`, arity 0 | Distinct from string overload and byref parameter |
| CLR-SIG-002 | String overload of `Select` | `(String) -> String`, arity 0 | Same method name does not collapse overloads |
| CLR-SIG-003 | `ref int` / `ByRef Integer` / `byref<int>` | `(Int32&) -> Int32`, arity 0 | Not a value parameter; no aliasing/runtime claim |
| CLR-SIG-004 | `Echo<T>` / `Echo(Of T)` / `Echo<'T>` | `(!!0) -> !!0`, arity 1 | Generic parameter name is not identity |
| CLR-SIG-005 | `Echo<TLeft,TRight>` and counterparts | `(!!0) -> !!0`, arity 2 | Same parameter/return types still differ by method arity |
| CLR-SIG-006 | `int[]` / `Integer()` / `int[]` overload of `Rank` | Vector input/return | Not rectangular array identity |
| CLR-SIG-007 | `int[,]` / `Integer(,)` / `int[,]` overload | Rank 2, no sizes, lower bounds `0,0` | Shape is metadata encoding, not observed array bounds |
| CLR-SIG-008–010 | Duplicate C#, VB, F# assembly at a second locator | `AmbiguousDuplicateManagedAssembly`, Tier4 | No unique source binding; declarations are still observations |
| CLR-SIG-011 | Each assembly truncated to 64 header bytes | `MalformedManagedInput`, Tier4; no method facts | Data-only read, never load or execute |
| CLR-SIG-012 | Each assembly with member limit 1 | `ManagedInputMemberCountLimitExceeded`, Tier4; no method facts | Rejection is partial coverage, not absence |

Positive cases use `dotnet.compiled.member.v1`, `Tier2Structural`. Rejections
use `dotnet.compiled.gap.v1`, `Tier4Unknown`, with input locators and the same
provenance envelope. The repeat test reverses input order and requires identical
serialized provenance and facts. No new derived machine-readable format or
checked-in generated golden is introduced; emitted facts retain the existing
exact-generator and bounded-input digest contract.

## Commands and lanes

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~Clr_signature' -warnaserror
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~ManagedMetadataExtractorTests' -warnaserror
```

The tests are a partial `ManagedMetadataExtractorTests` class, so the existing
`local-distribution-validation.yml` metadata filter runs them on Linux, macOS
and Windows with .NET 10; the full adapter-validation suite also includes them.
No ILAsm, Windows-only API, private corpus, or runtime loading is needed.
Source-adapter OSS smokes are deferred for this fixture/test-only change:
source extraction and production scanner code are unchanged. The full .NET
suite and CLI artifact smoke guard shared fixture consumers.

## Reconciliation at the fetched integration base

Base: `origin/dev` `9dde9bb5f400221e0d8502b5008d61884596230f`;
`origin/main` `7f026f5a9b59f9f3f2c1e203f1e8b001fdddc261` (2026-10-05).
All five issues are open, with no issue comments at reconciliation. No open PR
was found; existing worktrees contain other scopes. This lane targets `dev`,
consistent with compiled-evidence PRs #772–#788 and the integration policy.

| Requirement | Delivered implementation/test evidence | Remaining acceptance | Required host/toolchain |
| --- | --- | --- | --- |
| #769 dimensions | Corpus runway; Task 11 inventory/selection guards | Deduplicated private catalog, reviewed/minimized representatives, deterministic private/projected receipts | Authorized isolated Windows worker; unavailable to this run |
| #767 language matrix | Metadata foundation #772; source joins #773; PDB #774; messy fixtures #786/#787; this signature slice | Full reviewed language-specific matrix: optional/defaults, VB event/receiver interactions, F# options/constraints/quotations and targeted generated-member interactions | Public .NET 10 fixtures; no F# source-extraction claim |
| #766 IL/rewrite | #775–#783: operand-aware bodies, before/after edges, PDB, control-flow/member/topology suites and independent Windows ILAsm/ILDAsm | `IldasmPortablePdbLineOracleUnavailable` still blocks independent portable-PDB line parity; broader acceptance must remain bounded to catalog cases | Portable readers locally; Microsoft ILAsm/ILDAsm on Windows |
| #768 Windows | #785 runner/guards/feasibility study; #788 separate buildable-fix profile | No passing private bounded receipt; legacy dependencies and deferred native/mixed-mode C++/CLI shapes | Isolated Windows x64, Framework/MSVC; last recorded clean private build lacked pinned PostSharp/SQLite packages |
| #759 epic | Above merged ancestry is present in both fetched branches | Full child acceptance and private endurance evidence remain incomplete | Both public and authorized private lanes |

Historical status pages describe earlier slices; promotion to main establishes
ancestry only. The private dependency blocker is the last recorded result from
#788, not a new inspection or attempted private run. No Windows/private worker
was invoked for this slice. Tasks 10 and 11 remain unchecked.
