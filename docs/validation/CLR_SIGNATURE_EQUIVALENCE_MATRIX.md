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

## Optional-parameter agreement continuation

Base: `origin/dev` `e9212c53acbdfc83e8dbfc4a83c2272576715db2`, the verified
merge of #820. Branch: `codex/767-optional-parameter-evidence`. #767 remains
open; this extends the marker matrix, not default-value or full API equivalence.

`OptionalShape` in each public language fixture supplies a required parameter,
optional parameters with source defaults 7 and 9, and an eleven-parameter
optional method. C#/VB use their source optional syntax; F# uses explicit CLI
`Optional` and `DefaultParameterValue` attributes. This is not F# `?arg` /
`FSharpOption<T>` source semantics or F# source extraction.

The existing `optionalParameterOrdinals` property is consumed by source
reconciliation, so equal member identities are insufficient when the readers
disagree on these markers. `managed-metadata/0.1.3+cecil-0.11.6` compares that
property before admitting a member. SRM now sorts by numeric sequence number
before formatting the zero-based ordinals. For a setter-only indexed property,
it selects Param rows by signature sequence position, excluding the setter
value even when an index parameter has no Param row. These are additive
agreement checks; identity encoding and fact schemas are unchanged.

| Case | Input / oracle | Expected evidence / non-claim |
| --- | --- | --- |
| CLR-OPT-001 | Required and optional Int32 methods in C#/VB/F# | Equal signature goldens; markers empty versus `0`; three distinct endpoints for each named method |
| CLR-OPT-002 | Source defaults 7 versus 9 | Both markers are `0`; default-value equality is explicitly unclaimed |
| CLR-OPT-003 | Eleven optional parameters in each language | Exact `0,1,2,3,4,5,6,7,8,9,10`; no reader disagreement |
| CLR-OPT-004 | Injected method/property reader observations with equal identities but differing, missing or reordered markers | `CrossCheck` returns the disputed row and `MetadataReaderDisagreement`; identical markers are the positive control |
| CLR-OPT-005 | Cecil-produced setter-only property, unnamed/unflagged index parameter, optional value | Independent SRM oracle proves only Param sequence 2 exists; property marker is empty, setter method marker is `1` |
| CLR-OPT-006 | Same property with optional index | SRM proves sequences 1 and 2; property marker is `0`, setter method marker is `0,1` |

The positive fixtures use `dotnet.compiled.member.v1` / Tier2 with exact
commit, extractor version, metadata locations and generator/input commitments.
The ordinary disagreement path withholds disputed member rows and emits
`dotnet.compiled.gap.v1` / Tier4; it cannot invent a source join. The previous
CLR-SIG-008–012 duplicate, malformed-header and member-limit regressions run
against the expanded assemblies as well. The prior reversed-input test covers
byte determinism of all emitted facts, including the new optional methods.
No new derived machine-readable artifact is introduced.

Regression sequence: both disagreement tests failed on the merged base.
Enabling the comparison alone then failed the wide-method and sparse-setter
cases, proving the two SRM normalization defects before repair. The final
focused optional/signature filter passes 20/20.

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~Optional_parameter|FullyQualifiedName~Clr_signature' -warnaserror
```

The existing cross-platform metadata CI filter includes all five added test cases.
Source-adapter OSS smokes remain deferred because source extraction is
unchanged; required local adapter checks, full .NET tests and the compiled CLI
smoke are recorded in the implementation-state note. Windows/private-corpus
acceptance and the independent ILDAsm PDB oracle remain separate outstanding
work.

## Bound source optional-marker continuation

Base: `origin/dev` `a0de5398e38982a2ac606e06f102bc4f02a9a036`, the verified
#821 merge (2026-10-05); main remains `7f026f5a`. No open PR or competing
source-ordinal worktree was present. Branch: `codex/767-source-optional-ordinals`.

The optional review suggestion on #821 claimed lexical source ordering. Current
`SourceMetadataIdentityCandidate.OptionalParameterOrdinals` is an integer list;
`SourceMetadataReconciler` sorts integers before serialization. That suggestion
requires no production fix. Nine new test cases pin the missing source-join
coverage. A temporary lexical-sort mutation made all nine fail; the mutation
was removed. Production identities, rules, schemas and versions are unchanged.

| Requirement / case | Implementation and test evidence | Remaining gap | Host/toolchain |
| --- | --- | --- | --- |
| #767 CLR-SRC-OPT-001/002 | Bound C#/VB `OptionalShape` scans join all four methods, including eleven optional ordinals; exact marker goldens and both fact endpoints, source spans, metadata location, rule/tier, commit, versions and hashes | Marker equality does not prove default substitution or runtime behavior; no F# source adapter | .NET 10, portable |
| CLR-SRC-OPT-003 | Injected out-of-order integer source ordinals normalize numerically and repeat identically | Comparator fixture is not reader-admission evidence | .NET 10 |
| CLR-SRC-OPT-004–007 | Lexically ordered, missing, duplicated and malformed compiled marker strings refuse joins with `SourceMetadataOptionalParameterMismatch` | Synthetic rejected comparator inputs, not malformed PE coverage | .NET 10 |
| CLR-SRC-OPT-008/009 | Zero/two compiled candidates refuse joins; summary limit zero retains omission count/hash and input/generator commitments | Summary truncation does not remove the underlying facts or change their evidence | .NET 10 |
| #769 corpus dimensions | Existing runway/Task 11 guards unchanged | Reviewed private catalog/minimized representatives and repeat receipts remain unproven | Authorized isolated Windows worker unavailable to this run |
| #766 IL/rewrite | Existing operand-aware and independent-reader suites unchanged | Independent portable-PDB line oracle and broader catalog acceptance remain open | Portable readers plus Windows Microsoft tools |
| #768 Windows lane | Runner/guards and feasibility study unchanged | No passing private bounded receipt; recorded legacy dependency blockers remain historical | Authorized Windows/.NET Framework/MSVC lane |

The metadata-plus-source test filter now runs in the existing Linux/macOS/Windows
local-distribution workflow; the full Linux adapter suite also includes these
cases. Public F# assembly evidence remains separate from source reconciliation.
No private data, runtime execution or new machine-readable artifact is added.

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Optional_source_matrix'
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~ManagedMetadataExtractorTests|FullyQualifiedName~SourceMetadataReconciliationTests'
```
