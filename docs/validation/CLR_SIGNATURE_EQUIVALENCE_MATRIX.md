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

## Nullable and F# option continuation

Base: `origin/dev` `756368375582a9e0f10cceab18890e9905261b28`, verified #822
merge; `origin/main` remains `7f026f5a` (2026-10-05). Branch:
`codex/767-nullable-option-matrix`. No open PR or competing worktree for this
slice was present. The earlier signature/marker/source-join slices are delivered;
all five epic issues remain open.

The new public `OptionShape` types compare complete method signatures only.
Hand-authored goldens name the assembly scopes and instantiated generic types:
`System.Nullable<Int32>`, `Microsoft.FSharp.Core.FSharpOption<Int32>` and
`Microsoft.FSharp.Core.FSharpValueOption<Int32>`. The F# fixture pins
`FSharp.Core` package 10.1.302 (assembly 10.1.0.0), so a compiler SDK update cannot
silently change the option scope. The System.Runtime reference is 10.0.0.0.
A different supported reference identity requires an explicit matrix update.

| Case | Source construct / expected evidence | Counterexample or remaining gap | Host/toolchain |
| --- | --- | --- | --- |
| CLR-OPTION-001 | C# `int?`, VB `Integer?`, F# `System.Nullable<int>`: equal static roundtrip signatures, three distinct assembly/member/fact identities | Signature equality is not interchangeability or runtime behavior | Public .NET 10 |
| CLR-OPTION-002 | F# `int option`: exact FSharpOption generic signature | Distinct from Nullable and FSharpValueOption | .NET 10, pinned FSharp.Core |
| CLR-OPTION-003 | F# `int voption`: exact FSharpValueOption generic signature | Distinct metadata type; no allocation/layout/runtime claim | Same |
| CLR-OPTION-004 | F# `?value: int`, returned as `int option`: same signature as explicit option roundtrip; empty CLI optional-ordinal list | F# optional source syntax is not a CLI Optional Param flag; different method endpoints remain distinct | Same |
| CLR-OPTION-005 | Reversed three-assembly input order | Byte-identical provenance and facts, no guessed source edge | Same |
| CLR-OPTION-006–008 | Each language's expanded assembly copied to a duplicate locator, truncated to a 64-byte header, or admitted with member limit 1 | Explicit duplicate ambiguity, malformed-input and member-limit gaps; duplicate observations are ineligible for source reconciliation | Same; data-only reads |
| CLR-OPTION-009 | Bound F# scan retains all four OptionShape methods and the existing source-unsupported gap | No source-to-metadata edge; F# source extraction remains unsupported | Same |

Eight new metadata test cases plus the strengthened existing F# source-gap test
cover these rows. Positive facts require `dotnet.compiled.member.v1` / Tier2,
exact signature/endpoint, metadata token/location, commit, extractor version,
exact generator DLL hash and bounded-input hash. Rejections use
`dotnet.compiled.gap.v1` / Tier4 and the same provenance envelope. Cecil and SRM
must agree; no assembly is executed. No production extractor, rule, schema or
identity encoding is changed, and no new machine-readable artifact is added.

| Epic requirement | Delivered evidence | Remaining acceptance | Host/toolchain |
| --- | --- | --- | --- |
| #769 corpus dimensions | Existing runway and admission/selection guards | Reviewed private catalog, minimized representatives, deterministic private/projected receipts | Authorized isolated Windows worker unavailable to this run |
| #767 language matrix | #820 signatures, #821 optional markers, #822 bound C#/VB joins, this nullable/option slice | Defaults/caller semantics, further VB receiver/event and C#/F# generated-member/constraint/quotation interactions | Public .NET fixtures; no F# source claim |
| #766 IL/PDB/rewrite | Existing operand-aware and independent-reader catalog | Independent portable-PDB line oracle and broader reviewed acceptance | Portable readers; Windows Microsoft tools for oracle |
| #768 Windows lane | Existing runner, guards, feasibility study; public CI is separate | No passing private bounded receipt; historical legacy dependency blockers remain unverified here | Authorized isolated Windows/.NET Framework/MSVC |

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~Nullable_option_matrix|FullyQualifiedName~Fsharp_fixture_retains' -warnaserror
```

The existing portable metadata/source CI filter includes these tests on Linux,
macOS and Windows. Source-adapter pinned OSS smokes are deferred because this
slice changes only fixtures/tests and their documentation. Full-suite and CLI
results are recorded in the spec implementation state.

## Property/event accessor continuation

Base: `origin/dev` `c389b20dfc0fc3865f8faae12d3df74524031980`, verified #823
merge; main remains `7f026f5a` (2026-10-05). Branch:
`codex/767-property-event-matrix`. No open PR or competing worktree was found.

Each public language fixture adds `AccessorShape`: a read/write Int32 `Value`,
getter-only Int32 `Snapshot`, EventHandler `Changed`, and ordinary method
`get_Unbound`. C# and VB use inert custom events; F# exposes a CLIEvent. VB's
custom event additionally emits an explicit raiser. These are metadata fixtures,
not equivalent implementations of event delivery or backing storage.

The test oracle reads Property/Event accessor handles directly with
System.Reflection.Metadata and locates the corresponding already cross-checked
TraceMap method facts by assembly-local metadata token. It never infers an
association from `get_`, `set_`, `add_`, `remove_` or `raise_` name prefixes.
Both owning member and method retain separate endpoint identities. This is a
fixture oracle; no new production accessor-edge rule or runtime claim is added.

| Case | Expected evidence | Counterexample / non-claim |
| --- | --- | --- |
| CLR-ACCESSOR-001/002 | Matched `Value`/`Snapshot` property signatures across C#/VB/F#, three distinct assembly/member/fact identities per row | Their equal property signatures do not establish equal accessor availability: only Value has a setter |
| CLR-ACCESSOR-003 | Matched EventHandler event type with exact System.Runtime scope | Equal event type does not imply storage, delivery or raise behavior |
| CLR-ACCESSOR-004–006 | Raw metadata handles bind two getters, one setter, add/remove and, for VB only, the explicit raiser to exact method facts with golden instance signatures | No method-name association; nil/other accessor sets and unique role handles are asserted |
| CLR-ACCESSOR-004–006 decoy | Ordinary `get_Unbound` has the same getter-looking signature but no SpecialName flag or property association | A display/name heuristic cannot mint a property edge |
| CLR-ACCESSOR-007 | Reverse assembly input order | Byte-identical facts and compiled-input provenance |
| CLR-ACCESSOR-008–010 | Each expanded language assembly duplicated, header-truncated, or member-limited | Explicit ambiguity/malformed/limit gaps; duplicate observations cannot participate in source reconciliation |
| CLR-ACCESSOR-011 | Bound F# scan retains both properties and event | Existing explicit unsupported-source gap remains; no source identity edge |

Ten new metadata tests plus the strengthened existing F# test cover these rows.
Assertions retain `dotnet.compiled.member.v1` / Tier2, owner/method endpoints,
0x17/0x14/0x06 property/event/method tokens, metadata locations, commit,
extractor version, exact generator DLL hash and bounded-input hash. Rejection
facts use `dotnet.compiled.gap.v1` / Tier4 and the same provenance envelope.
No binary is executed, production formatter changed, or new machine-readable
artifact introduced. The existing portable CI metadata/source filter includes
all cases on Linux, macOS and Windows.

| Requirement | Delivered evidence | Remaining gap | Required host/toolchain |
| --- | --- | --- | --- |
| #769 dimensions | Existing inventory/admission/selection guards | Reviewed private catalog, representatives and repeat receipts remain unproven | Authorized isolated Windows worker unavailable here |
| #767 language matrix | #820–#823 signatures, markers, joins and options; this accessor matrix | Defaults, constraints, generated-member and VB receiver/event interactions beyond these cases | Public .NET 10; no F# source claim |
| #766 IL/PDB/rewrite | Existing operand-aware and independent-reader catalog | Independent portable-PDB line oracle and broader reviewed acceptance | Portable readers plus Windows Microsoft tools |
| #768 Windows lane | Existing runner and feasibility guards; public CI separate | No passing private bounded receipt; recorded legacy dependency blockers not revalidated here | Authorized Windows/.NET Framework/MSVC |

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~Property_event_matrix|FullyQualifiedName~Fsharp_fixture_retains' -warnaserror
```

Pinned source-adapter OSS smokes are deferred for this fixture/test-only change.
All five epic issues and Tasks 10/11 remain open. Exact validation commands and
results live in the spec implementation state.

## Generic-constraint continuation

Base: verified #824 merge `5adfaeb6fad63ecade0bab29a0fabf682ebd38fa` on
`origin/dev`; main remains `7f026f5a`. Branch `codex/767-generic-constraint-matrix`.
No open PR or competing worktree was found; unrelated changes were preserved.

`ConstraintShape` exposes five static identity-shaped generic methods in each
public C#/VB/F# assembly. All fifteen share the golden method signature
`arity:1|call:default|hasThis:false|explicitThis:false|(!!0)->!!0`, but their
constraints differ. Equal signatures do not prove equal instantiation rules.

| Case | Source construct / expected raw CLR evidence | TraceMap evidence and non-claim |
| --- | --- | --- |
| CLR-CONSTRAINT-001 | Free: flags 0, no constraint rows | Three distinct assembly/member endpoints with equal signatures |
| CLR-CONSTRAINT-002 | Reference: flags 4, no constraint rows | Reference constraint is oracle evidence, not a signature component |
| CLR-CONSTRAINT-003 | Value: C#/VB flags 24 plus System.ValueType; F# flags 8 with no constraint row | Similar source constructs are not identical metadata encodings |
| CLR-CONSTRAINT-004 | Construct: flags 16, no constraint rows | No object construction or runtime admissibility is tested |
| CLR-CONSTRAINT-005 | Disposable: flags 0, System.IDisposable constraint | Exact System.Runtime 10.0.0.0 scope and token association |
| CLR-CONSTRAINT-006 | Fifteen distinct endpoints, one signature shape, reversed input order | Byte-identical facts/provenance; no cross-language identity collapse |
| CLR-CONSTRAINT-007–009 | Duplicate, truncated and member-limited inputs for each language | Ambiguity, malformed and limit gaps; no eligible duplicate reconciliation |
| CLR-CONSTRAINT-010 | Bound F# retains all five named method signatures | Explicit unsupported-source gap; no F# source identity edge |

Nine new metadata tests and the strengthened existing F# test cover these rows.
SRM validates GenericParam owner/index/flags and GenericParamConstraint owner,
type and assembly scope; Cecil independently reads flags and constraint names.
Exact assembly-local MethodDef tokens join the oracle observations to TraceMap
facts from that same input. Facts retain rule `dotnet.compiled.member.v1`, Tier2,
metadata locations, endpoints, commit, extractor version, exact generator hash
and bounded-input hash. Rejections retain `dotnet.compiled.gap.v1` / Tier4.

This is a fixture oracle, not a production generic-constraint fact or complete
constraint-equivalence engine. Generic constraints are not currently included
in TraceMap's normalized method signatures; this remains a documented coverage
gap, not a clean constraint comparison. No F# source, PDB, rewritten-body,
runtime, variance, unmanaged/notnull or static-member constraint claim is made.
No new machine-readable artifact or production schema/version is introduced.

| Requirement | Implementation/test evidence | Remaining gap | Host/toolchain |
| --- | --- | --- | --- |
| #769 dimensions | Existing inventory/admission and synthetic guards | Reviewed private catalog and representative receipts unproven | Authorized Windows/private lane unavailable |
| #767 language matrix | #820–#824 signatures/options/accessors; current constraint oracle | Production constraint facts, defaults, generated-member and language interactions | Public .NET 10; F# source unsupported |
| #766 IL/PDB/rewrite | Operand-aware suite and independent reader tests | Broader reviewed matrix and independent PDB line oracle | Portable readers; Windows tools for remaining oracle |
| #768 Windows | Existing bounded runner and public CI | Passing private bounded receipt and full feasibility acceptance | Authorized Windows/.NET Framework/MSVC unavailable |

Focused command: `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~Generic_constraint_matrix|FullyQualifiedName~Fsharp_fixture_retains' -warnaserror`.
The existing CI filter includes the new cases on Linux/macOS/Windows. Exact
validation results and explicit deferrals are in the spec implementation state.
All epic issues and Tasks 10/11 remain open.

## Default-value continuation

Base: verified #825 merge `74364fe5eb4168757d508a232d59958d21eb73a3` on
`origin/dev`; main remains `7f026f5a`. Branch `codex/767-default-value-matrix`.
No open PR or competing worktree was found. Existing worktrees and unrelated
Base44 changes were preserved.

Six `DefaultShape` methods in each public C#/VB/F# fixture separate parameter
optionality, Constant-table defaults and attribute-encoded defaults. F# uses
explicit CLI attributes; this does not establish F# source calling semantics.

| Case | Source/CLR oracle | TraceMap evidence and limitation |
| --- | --- | --- |
| CLR-DEFAULT-001 | Required Int32, no Optional/HasDefault flags or Constant row | Empty optional ordinals; no default is distinct from a null default |
| CLR-DEFAULT-002/003 | Optional Int32 7/9: Constant type Int32, blobs 07000000/09000000 | Equal signatures and optional ordinal 0, distinct endpoints and defaults |
| CLR-DEFAULT-004 | Optional string seven: exact UTF-16 constant bytes | String signature and optional ordinal 0; no runtime substitution claim |
| CLR-DEFAULT-005 | Optional null string: NullReference code and four zero bytes | Same string signature, different default; null is not an absent row |
| CLR-DEFAULT-006 | Decimal 7: Optional flag, no HasDefault/Constant row, exact DecimalConstantAttribute constructor and blob | Decimal signature retains System.Runtime scope; absence of a Constant row is not absence of a default |
| CLR-DEFAULT-007 | Reversed assembly inputs | Byte-identical facts/provenance; six integer methods retain six endpoints despite equal signature/markers |
| CLR-DEFAULT-008–010 | Duplicate, header-truncated and member-limited inputs per language | Explicit ambiguity/malformed/limit gaps; no eligible duplicate reconciliation |
| CLR-DEFAULT-011 | Bound F# retains all six named signatures and optional markers | Explicit unsupported-source gap; no F# source reconciliation edge |

Ten new metadata cases and the strengthened bound F# test cover these rows.
SRM checks parameter flags, exact Constant parent/type/blob, and decimal
attribute parent/constructor/scope/blob. Cecil independently checks decoded
constant values and decimal constructor arguments. Exact MethodDef tokens from
the same input bind these observations to both single-input and combined-input
TraceMap facts, with exact token/signature/optional-marker parity asserted.
The constraint matrix pins the same cross-input invariant. No fixture assembly
is loaded or executed. All facts retain `dotnet.compiled.member.v1`, Tier2,
metadata locations, endpoints, commit, extractor version, exact generator and
bounded-input hashes; rejection facts retain `dotnet.compiled.gap.v1` / Tier4.

Default values are not currently production TraceMap fact properties or method
identity components. This matrix proves the fixture oracle and explicitly
retains that production coverage gap; it does not prove complete default-value
comparison, source-to-default reconciliation, caller behavior, enum/date/floating
point defaults, arbitrary attributes, PDB/IL/rewrite equivalence or F# source
support. No production schema, version or new derived artifact is introduced.

| Requirement | Implementation/test evidence | Remaining gap | Host/toolchain |
| --- | --- | --- | --- |
| #769 corpus dimensions | Existing inventory/admission and synthetic guards | Reviewed private catalog and representative receipts | Authorized Windows/private lane unavailable |
| #767 language matrix | #820–#825 signatures/options/accessors/constraints; current default-value oracle | Production default/constraint facts, generated-member and language interactions | Public .NET 10; no F# source claim |
| #766 IL/PDB/rewrite | Operand-aware and independent-reader suite | Broader reviewed matrix and independent PDB line oracle | Portable readers plus Windows tools |
| #768 Windows | Existing bounded runner and public CI | Passing private bounded receipt and feasibility acceptance | Authorized Windows/.NET Framework/MSVC unavailable |

Focused command: `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~Default_value_matrix|FullyQualifiedName~Fsharp_fixture_retains' -warnaserror`.
Existing portable CI includes these cases on Linux, macOS and Windows. Exact
results and deferrals are in the spec implementation state. All epic issues and
Tasks 10/11 remain open.

## Explicit-interface continuation

Base: verified #826 merge `b40267b167b46910f6846bb6f0db6e80d8aa58f4` on
`origin/dev`; main remains `7f026f5a`. Branch `codex/767-explicit-interface-matrix`.
No competing open PR or active worktree was found; unrelated edits were retained.

Each public assembly declares ISharedFormatter.Format(String) and ExplicitShape
with an explicit implementation plus an ordinary public Format(String) decoy.
All three methods have equal normalized signatures but different identities.
VB names its implementing method FormatContract; C# and F# use the qualified
interface name. Raw MethodImpl rows, not names, establish the fixture association.

| Case | Expected raw metadata | TraceMap evidence and limitation |
| --- | --- | --- |
| CLR-INTERFACE-001 | C# InterfaceImpl/MethodImpl bind the interface declaration to its private explicit method | Exact type/method tokens, qualified method name, virtual/new-slot/final flags; ordinary Format is not the implementing body |
| CLR-INTERFACE-002 | VB binds Format to differently named FormatContract | Same signature does not collapse declaration, implementation or public decoy |
| CLR-INTERFACE-003 | F# binds the qualified implementation with virtual/new-slot but no Final flag | Preserve the emitted flag difference; no complete class/dispatch equivalence claim |
| CLR-INTERFACE-004 | Nine distinct method endpoints/fact IDs across three assemblies with equal signatures | Reversed inputs produce byte-identical facts/provenance |
| CLR-INTERFACE-005–007 | Duplicate, header-truncated and member-limited inputs in each language | Explicit ambiguity/malformed/limit gaps and no eligible duplicate reconciliation |
| CLR-INTERFACE-008 | Bound F# retains the declaration, implementation and decoy with exact names/signatures | Unsupported-source gap remains; no F# source edge |
| CLR-INTERFACE-009 | Single/combined scans reject another assembly's raw hash or an incorrect binding hash, including jointly corrupted fact/outcome raw hashes | Six oracle regressions across all three languages; raw hashes come from the actual fixture bytes |

Thirteen new metadata cases and the strengthened F# test cover these rows. SRM
checks InterfaceImpl and MethodImpl ownership and raw declaration/body MethodDef
handles; Cecil independently checks interface and override tokens and flags.
Single-input and combined-input facts must match exact raw tokens, owning type,
method name, signature and optional markers. The shared member helper binds each
fact to the matching input outcome, assembly identity, actual fixture-byte SHA-256
and per-input binding SHA-256; accessor, constraint and default matrices inherit
the same check. Their rule/tier, metadata location,
commit, extractor version, exact generator and bounded-input hashes remain
asserted (`dotnet.compiled.member.v1` / Tier2; gaps use
`dotnet.compiled.gap.v1` / Tier4). No binary is loaded or executed.

This is a fixture oracle, not a new production MethodImpl/dispatch edge. It does
not prove runtime dispatch, generic interface construction, default interface
methods, PDB/rewritten-body ownership, equivalent class sealing, or F# source
support. C#/VB classes are sealed; F# is not. No production schema, extractor
version or new derived artifact is introduced.

| Requirement | Implementation/test evidence | Remaining gap | Host/toolchain |
| --- | --- | --- | --- |
| #769 corpus dimensions | Existing inventory/admission and synthetic guards | Reviewed private catalog and representative receipts | Authorized isolated Windows/private lane unavailable |
| #767 language matrix | #820–#826 signatures/options/accessors/constraints/defaults; current explicit-interface oracle | Generated members and remaining language interactions; production relationship/default/constraint facts | Public .NET 10; F# source unsupported |
| #766 IL/PDB/rewrite | Operand-aware and independent-reader suite | Broader reviewed matrix and independent PDB line oracle | Portable readers plus Windows tools |
| #768 Windows | Existing bounded runner and public CI | Passing private bounded receipt and full feasibility acceptance | Authorized Windows/.NET Framework/MSVC unavailable |

Focused command: `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~Explicit_interface_matrix|FullyQualifiedName~Fsharp_fixture_retains' -warnaserror`.
Existing CI includes these cases on Linux/macOS/Windows. Exact commands, results
and deferrals are recorded in the spec state. All epic issues and Tasks 10/11
remain open.

## Operator and conversion continuation

Base: verified #827 merge `c155a35f3ed111f03fd88e2957e0326093a5eb7b` on
`origin/dev`; main remains `7f026f5a`. Branch
`codex/767-operator-conversion-matrix`. No competing open PR was found;
unrelated worktrees and Base44 edits remain untouched.

The public OperatorShape fixtures compile addition, implicit/widening conversion
from Int32, explicit/narrowing conversion to Int32, and an ordinary source method
named op_LooksLikeOperator. The latter shares addition's signature but retains
its own endpoint. F# marks even this ordinary op_-prefixed method SpecialName;
C#/VB do not. Neither spelling nor that flag establishes operator semantics.

| Case | Expected metadata/source evidence | Non-claim or explicit gap |
| --- | --- | --- |
| CLR-OPERATOR-001–003 | Each language has four exact MethodDef tokens, public/static flags, raw return/parameter types and a declared SpecialName expectation | F# decoy flag differs; no runtime invocation or operator resolution claim |
| CLR-OPERATOR-004 | Corresponding operators retain assembly-scoped self types, twelve distinct method/fact IDs and three distinct canonical signatures per method name | No cross-assembly identity collapse; ordinary decoy is distinct despite equal within-assembly signature |
| CLR-OPERATOR-005–007 | Duplicate assemblies, truncated PE headers and member-count limit inputs remain explicit gaps in every language | Duplicate declarations are ineligible for reconciliation; rejected inputs produce no selected methods |
| CLR-OPERATOR-008–009 | C#/VB bound source joins retain source/metadata endpoint identities, fact references, source spans, rule/tier, commit and extractor versions | Receipt-bound declaration joins do not prove runtime conversion behavior |
| CLR-OPERATOR-010 | F# compiled signatures remain available under a bound scan | F# source reconciliation is unsupported; no guessed edge |

SRM independently reads raw method signature headers, parameter counts, primitive
type codes and exact self-type handles. Cecil independently checks method/type
tokens, return/parameter types and flags. Both single and combined TraceMap scans
must match hand-authored, assembly-scoped signature goldens and exact metadata
identities/tokens. The shared member evidence helper checks rule/tier, location,
commit, extractor version, exact generator/bounded-input SHA-256, raw fixture-byte
hash and per-input binding hash. Reverse input order must yield identical facts
and provenance. No new derived artifact, production rule/schema/version, IL body,
PDB association, source operator-classification rule or conversion edge is added.
Checked/lifted operators, overload resolution and runtime conversions remain open.

| Requirement | Verified implementation/test evidence at base | Remaining acceptance gap | Host/toolchain |
| --- | --- | --- | --- |
| #769 corpus dimensions | Historical runway inventory; separate bounded Windows runner | Authorized catalog/minimization and reproducible privacy-projected outputs | Authorized private corpus and isolated Windows toolchain; not invoked |
| #767 public language matrix | #820–#827 signatures, optional/defaults, nullable/options, accessors, constraints and explicit-interface fixtures; this slice adds operators/conversions | Remaining language interactions and full reviewed matrix; F# source extraction unsupported | .NET SDK 10.0.302 locally; public cross-platform CI |
| #766 IL/PDB/rewrite suite | Operand-aware identities, independent reader checks and public mutation lanes already merged | Complete reviewed edge matrix and independent PDB parity | Portable .NET plus declared Windows ILAsm/PDB lanes |
| #768 Windows/private lane | Public Windows CI, bounded runner guards and C++/CLI feasibility inventory | Passing private bounded receipt, corpus endurance and full feasibility acceptance | Existing authorized isolated Windows worker; unavailable here |
| #759 parent | Separate source/metadata/PDB/IL/rewrite evidence implementations and bounded validations | Child acceptance criteria remain open; merging a slice does not close the epic | Combination of public and authorized private lanes |

## Module and currying continuation

Base: verified #828 merge `6f168354a17a90dd16236105e9b1c366d4b76198` on
`origin/dev`; main remains `7f026f5a`. Branch `codex/767-module-currying-matrix`.
No competing open PR was found. Other worktrees and unrelated edits are preserved.

ModuleShape is a C# static class, VB Module, or F# module. All have Curried,
Tupled and Renamed methods. The C#/VB methods are ordinary static functions;
only F# has source currying and a tupled source argument. F# CompiledName maps
lowercase/sourceAlias declarations to the three selected metadata names.

| Case | Expected evidence | Limitation / negative assertion |
| --- | --- | --- |
| CLR-MODULE-001 | C# sealed/abstract container; three public static MethodDefs with exact raw signatures, tokens and full identities | Static-class structure does not prove source module semantics |
| CLR-MODULE-002 | VB sealed/non-abstract container with independently decoded StandardModuleAttribute; same static signatures | Preserve container flag difference; scoped attribute constructor identity and blob checked |
| CLR-MODULE-003 | F# sealed/abstract container; Curried and Tupled have identical two-Int32 parameter signatures | Only Curried has CompilationArgumentCounts `[1,1]`; CompiledName is consumed; CompilationSourceName independently retains curried, tupled and sourceAlias |
| CLR-MODULE-004 | Nine distinct method/fact IDs across three assemblies; reversed inputs preserve exact facts/provenance | Signature equality does not collapse endpoints or prove source calling conventions; sourceAlias/lowercase aliases are not guessed metadata members |
| CLR-MODULE-005–007 | Duplicate assemblies, truncated PE and member-count limits in all three languages | Explicit ambiguity/malformed/limit gaps; duplicate methods ineligible for source reconciliation |
| CLR-MODULE-008–009 | Bound C#/VB source declarations join exact metadata endpoints | Both evidence envelopes remain; no runtime or F# source claim |
| CLR-MODULE-010 | Bound F# scan retains all three compiled names/signatures | Unsupported-source gap remains; no production source-name or source-currying edge |
| CLR-MODULE-011 | Independent C#/VB declaration identities and exact fixture spans for modules, operators and optional parameters | Self-consistent substitutions of another method's source symbol, declaration or span are rejected |
| CLR-MODULE-012 | Source reconciliation fixtures resolve the running test assembly configuration | Release must work with Debug fixture directories unavailable |

SRM reads exact signature and attribute blobs, parent handles, constructor
signatures and assembly scopes; Cecil independently checks tokens, flags,
parameter/return types and decoded argument arrays. FSharp.Core is pinned by the
fixture package and metadata oracle (assembly 10.1.0.0); the VB attribute scope
is Microsoft.VisualBasic.Core 15.0.0.0. Single/combined facts require exact full
assembly/member identities, metadata tokens and golden signatures. The shared
member helper checks rule/tier, metadata location, commit, extractor version,
exact generator/bounded-input SHA-256, raw fixture bytes and per-input binding
hash. Source joins preserve source spans, separate endpoints and binding evidence.

This adds a fixture oracle, not a production module/currying classifier or
attribute relationship rule. CompilationSourceName retains source aliases in
metadata, independently decoded by SRM and Cecil. An alias alone does not establish
a physical source declaration, location or ownership edge. No runtime execution, PDB mapping,
F# source extraction, schema/version change or new derived artifact is introduced.

| Requirement | Verified implementation/test evidence | Remaining gap | Host/toolchain |
| --- | --- | --- | --- |
| #769 corpus | Historical inventory/runway and isolated runner contracts | Private catalog, reviewed minimization and deterministic projected artifacts | Authorized isolated Windows/corpus access; unavailable and uninvoked |
| #767 language matrix | #820–#828 public signatures, defaults/options/constraints, accessors, explicit interfaces and operators; this slice adds modules/currying | Other language interactions and full reviewed acceptance; no F# source adapter | .NET SDK 10.0.302 locally plus public cross-platform CI |
| #766 IL/PDB/rewrite | Existing operand-aware evidence, independent readers and public mutation lanes | Broader edge matrix and independent PDB parity | Portable .NET and declared Windows ILAsm/PDB lanes |
| #768 Windows | Existing public CI and bounded runner guards | Private bounded receipt, endurance and complete C++/CLI acceptance | Authorized private Windows worker |
| #759 parent | Distinct evidence layers and bounded public validation | All child acceptance criteria remain open | Public and separately authorized private lanes |

## Record-generated member continuation

Branch `codex/767-record-generated-matrix` started at #828 `6f168354`, then
integrated fresh origin/dev after #829 merged at `874db8a9`. Module/currying
evidence is now delivered in dev. Main remains `7f026f5a`.

The new `RecordMatrix` public fixtures use a C# sealed positional record, an F#
record and an ordinary sealed VB class. All expose Count plus object overrides.
Only selected CLR signatures compare equally; the VB Equals implementation is
intentionally different. No runtime equality, hashing or formatting is claimed.

| Case | Implementation/test evidence | Boundary |
| --- | --- | --- |
| CLR-RECORD-001 | C# Equals(Object), GetHashCode and ToString raw MethodDef signatures and generated attributes | Self-typed Equals and other helpers are separate endpoints |
| CLR-RECORD-002 | VB ordinary object overrides have the same three signatures without generated markers | Method names/signatures do not imply record semantics |
| CLR-RECORD-003 | F# record object overrides retain generated markers; raw SRM/Cecil token, flag and return-type agreement | No F# source extraction or runtime claim |
| CLR-RECORD-004 | Nine separate method/fact IDs; independent full assembly/member identities and reversed-input determinism | Cross-assembly signature equality never merges identities |
| CLR-RECORD-005–007 | Duplicate, truncated PE and member-limit cases in each language | Explicit ambiguity/malformed/limit gaps; no guessed source eligibility |
| CLR-RECORD-008 | Bound F# scan retains generated record methods and the unsupported-source gap | Generated metadata does not establish physical source ownership |

The oracle reads raw primitive signature bytes, exact declaring type/method
handles, public/virtual/instance flags, and CompilerGeneratedAttribute parent,
constructor signature, assembly scope and value. Cecil independently decodes the
same bounded public input. Single/combined TraceMap assertions pin exact identity,
metadata token, signature, marker and optional ordinals. Shared provenance checks
retain rule ID, tier, metadata location, commit, extractor version, generator hash,
bounded-input hash and the specific fixture's raw/binding hashes.

No new production rule, schema, golden artifact or extractor version is introduced.
No fixture is executed. Attributes are structural evidence, not trusted proof of
source authorship or language origin; the compiler-generated marker is explicitly
compared against these controlled fixture sources only.

| Requirement | Delivered evidence | Remaining gap | Required host |
| --- | --- | --- | --- |
| #769 | Corpus runway and guarded runner contracts | Private dimension catalog, reviewed minimization and repeatable projected output | Authorized isolated Windows corpus lane; unavailable/uninvoked |
| #767 | Merged signatures/defaults/options/accessors/constraints/interfaces/operators/modules; this record slice | Union/quotation and other targeted generated-member/receiver interactions; full acceptance | Public .NET SDK 10.0.302 plus CI |
| #766 | Operand-preserving IL, PDB evidence and public rewrite mutations | Broader edge matrix and independent PDB parity | Public .NET/Windows IL tools |
| #768 | Public Windows CI and private-runner fail-closed guards | Passing private bounded receipt, endurance and full C++/CLI acceptance | Separately authorized private Windows worker |
| #759 | Distinct evidence layers and bounded fixture suites | Child criteria remain open | Public and private lanes remain separate |

## Union-factory continuation

Base: #830 merge `f77773472691c31764b16fd611a4356a126103f8` on origin/dev;
main remains `7f026f5a`. Branch `codex/767-union-factory-matrix`. No competing
open PR or active union lane was found; unrelated worktrees and edits are preserved.

The F# UnionMatrix has Ready and Failed(Int32) cases. C#/VB ordinary classes
expose similarly named Ready and NewFailed factories. Their self-return types
are assembly-scoped: similar signature shapes are not equal signatures or union
semantics. The ordinary factories intentionally do not implement a union.

| Case | Oracle/evidence | Boundary |
| --- | --- | --- |
| CLR-UNION-001–003 | Exact declaring type/MethodDef token, static/public flags, raw class-return/Int32 parameter signature, Cecil decoding and single/combined identities for each language | Six endpoints remain distinct; no display-name join |
| CLR-UNION-004 | F# type mapping SumType (1), case mapping UnionCase (8), factory ordinals Ready=0 and Failed=1; no mapping on C#/VB lookalikes | Independently pin attribute parent, constructor types/scope, blob fields and named-argument count; no runtime tag/dispatch claim |
| CLR-UNION-005 | Reversed input order preserves byte-identical facts/provenance | Signature scopes and generated markers remain distinct |
| CLR-UNION-006–008 | Duplicate assemblies, truncated PE and member limits in all languages | Explicit ambiguity/malformed/limit gaps and source-ineligible duplicates |
| CLR-UNION-009 | Bound F# scan retains both factory signatures and unsupported-source gap | Metadata mapping is not physical source extraction or source ownership |
| CLR-UNION-010 | Cecil attribute oracle resolves only the declared pinned FSharp.Core asset; wrong version and System.Runtime requests fail | No ambient dependency probing; enum values/underlying type are checked against that asset |

SRM reads the exact factory signatures and attribute blobs; Cecil independently
reads types, tokens, signatures, markers and decoded constructor arguments.
The enum-valued mapping attribute requires FSharp.Core for Cecil decoding. The
test-only resolver selects the fixture's locked FSharp.Core 10.1.302 restore asset
(`lib/netstandard2.1/FSharp.Core.dll`), verifies full assembly identity 10.1.0.0,
and refuses other identities. Missing/multiple assets fail the test explicitly.
This does not change production dependency admission or add attribute facts.

TraceMap assertions retain rule ID, tier, full assembly/member endpoints, metadata
location, commit, extractor version, generator/bounded-input hashes and exact
fixture raw/binding hashes through the shared evidence oracle. No new derived
artifact, production rule/schema or runtime execution is introduced. Attributes
are structural metadata, not authenticity or physical-source ownership proof.
Full union helper/layout, generic/struct/null-representation, quotation and
runtime semantics remain separate gaps.

| Requirement | Delivered evidence | Remaining gap | Host/toolchain |
| --- | --- | --- | --- |
| #769 | Runway and guarded runner contracts | Reviewed private dimension catalog, minimization and deterministic projected output | Authorized isolated Windows corpus access; unavailable/uninvoked |
| #767 | Merged signatures/defaults/options/accessors/constraints/interfaces/operators/modules/selected records; this union-factory slice | Further generated-member, quotation, receiver and full language acceptance | Public .NET SDK 10.0.302 and declared CI lanes |
| #766 | Operand-aware IL/PDB/rewrite evidence and public mutations | Broader edge matrix and independent PDB parity | Public .NET plus Windows tools where declared |
| #768 | Public Windows CI and private-runner guards | Passing private bounded receipt/endurance and complete C++/CLI acceptance | Separately authorized Windows worker |
| #759 | Distinct evidence layers and bounded public tests | All child acceptance criteria remain open | Public and private lanes remain distinct |
