# Compiled .NET Evidence Foundation Design

## Decision

Implement a small `Mono.Cecil`-backed managed metadata reader behind an explicit
compiled-input boundary. Cross-check normalized assembly/member identities with
`System.Reflection.Metadata`. Keep compiled facts in a separate evidence lane
and join them to source facts only through later rule-backed reconciliation.

## First-slice evidence contract

The first-slice rule families must be named and documented before code lands:

| Purpose | Proposed rule | Tier | Required limitation |
| --- | --- | --- | --- |
| Managed input admitted and hashed | `dotnet.compiled.input.v1` | Tier2Structural | Admission proves inspected bytes only, not freshness or source ownership. |
| Assembly/module identity | `dotnet.compiled.assembly.v1` | Tier2Structural | Metadata identity is not authenticity or runtime load evidence. |
| Type/member declaration | `dotnet.compiled.member.v1` | Tier2Structural | Declaration does not prove execution, dispatch, or reachability. |
| Missing, stale, unreadable, unbound, mismatched, ambiguous, disagreed, or bounded input | owning rule plus categorical gap | Tier4Unknown | The gap reduces only the coverage it actually bounds. |

Final rule names must be added to the rule catalog with explicit limitations.
Compiled facts must carry a compiled-specific extractor and coverage label; an
existing source fact is never re-tiered because matching metadata exists.

The later reconciliation slice, not the first implementation slice, owns the
proposed `dotnet.compiled.source-identity.v1` rule. That rule may emit an exact
source/metadata identity edge only with complete documented identity and
provenance inputs; ambiguity must fail closed. The first slice must neither
define that rule as active nor emit source-to-metadata edges.

## Provenance model

Every scan that evaluates compiled inputs emits a manifest-level
`compiledInputProvenance` section before facts are identified. The section is
unconditional: an empty admitted set still records the policy/schema version,
exact generator SHA-256, extractor IDs/versions, expected-input declarations,
effective bounds, deterministic candidate/admission outcomes, ordered
provenance-binding-input digests, and the bounded-input-set SHA-256 for that
artifact view. Missing, rejected, unreadable, unsupported, and over-budget
outcomes therefore retain generator and bounded-input provenance even though
no per-input admission record exists.

Each compiled input record contains:

- safe input locator and input role;
- raw file SHA-256 in the local/private admission record only, or a
  privacy-projected-input SHA-256 in a shareable projection;
- bounded-input-set SHA-256;
- provenance-binding-input SHA-256 over the canonical privacy-projected
  receipt/binding fields used to classify provenance;
- exact invoked generator SHA-256;
- extractor ID and version, including the pinned Mono.Cecil version;
- normalized assembly and module identity plus MVID when readable;
- optional binary-source repository and commit only when a validated receipt
  binds them;
- optional binary build/toolchain identity when a validated receipt supplies
  it; and
- coverage state and categorical gaps.

Every compiled `CodeFact` also carries the required scan repository and commit
in its ordinary envelope. These identify the repository snapshot TraceMap is
inspecting, not the source or build provenance of an admitted binary. Optional
receipt-validated fields such as `binarySourceRepository`,
`binarySourceCommitSha`, and `binaryBuildIdentity` carry that separate claim.
An unbound in-tree or external assembly therefore retains valid scan identity
without being mislabeled as output of the scan commit.

The first slice does not create shareable `CodeFact` or index projections from
a private repository or access-controlled compiled input. Their ordinary
`facts.ndjson` and `index.sqlite` remain local-only because the required scan
repository and commit are private identities. A separately generated
shareable summary, if produced, uses a distinct non-`CodeFact` schema and
artifact identity computed only from its privacy-projected inputs; it omits raw
repository, commit, path, receipt, and assembly identities. Designing a
privacy-safe shareable scan envelope for fact/index rows is a later reviewed
contract, not an implied substitution into `CodeFact.Repo` or
`CodeFact.CommitSha`.

The local/private bounded-input-set digest commits to the admission policy,
expected-input declarations, effective size/count/work limits, deterministic
ordered candidate/admission outcomes, admitted safe locators, roles, raw file
digests, and each provenance-binding-input digest. The provenance-binding input
includes every receipt field that can change source association or freshness
classification, such as the safe source identity, source commit, build
identity, declared assembly mapping, and receipt schema/version.

A shareable projection must omit the raw file digest for an access-controlled
or private assembly. It first applies the documented privacy projection to the
admitted input record, computes a privacy-projected-input SHA-256 only over
that canonical projected payload (excluding digest fields), and then recomputes
the bounded-input-set and provenance-binding-input digests only from
privacy-projected fields. It never
hashes or republishes a private source artifact, receipt, source digest, path,
identifier, or raw assembly digest. The projection algorithm and omitted or
replaced fields are part of the rule's documented limitations.

The bounded-input-set SHA-256 for the emitted local fact/index view participates
in `scanId` before any fact IDs are derived. Thus changes to external managed
inputs, admission policy, effective limits, or zero-admission outcomes cannot
silently reuse the identity of a materially different compiled-evidence scan.
A distinct shareable summary uses its privacy-projected bounded-input digest in
its own artifact identity and never reuses a scan identity derived from private
assembly bytes.

Freshness states are `bound`, `stale`, `unbound`, `mismatch`, `missing`, and
`unknown`. Only a validated build/scan receipt can establish `bound`, `stale`,
or `mismatch`. `stale` requires evidence that the binary is bound to an older
source commit than the source snapshot under analysis. Timestamps may be
reported locally as diagnostics but cannot set those states.

## Identity model

Keep these nodes distinct:

1. source declaration/symbol;
2. compiled assembly and module;
3. metadata type/member/signature/token;
4. PDB document/method/sequence point (later slice);
5. IL method body and operands (later slice); and
6. rewritten assembly/member/body (later slice).

The normalized metadata key includes assembly/module identity, declaring type,
member kind, member name, generic arity, calling convention, full return and
parameter signatures, field type, event type, and readable custom modifiers on
each applicable type position. Metadata tokens are locations within one module,
not portable global identities. MVIDs distinguish modules but do not bind them
to source by themselves.

Metadata-only facts use binary-location convention `managed-metadata-v1`
without pretending that a PDB or source span exists. The existing
`EvidenceSpan` serializes the admitted safe locator as `FilePath`, uses `1..1`
as a documented non-source sentinel, and leaves `SnippetHash` null. Properties
carry `evidenceLocationKind=managed-metadata-v1`, the module MVID when readable,
and `metadataToken=0x` followed by exactly eight lowercase hexadecimal digits
(for example, `0x02000001`) for row-backed facts; a deterministic metadata
offset is optional only when directly exposed by a reader. An
input-level gap without a row uses `managed-input-v1` and omits token/offset.
Human and machine consumers must label both forms as binary locations and must
not render the sentinel as source line evidence.

## Bounded dependency resolution

The first slice never invokes Mono.Cecil's ambient/default resolver. Resolution
is limited to explicitly declared dependency inputs admitted through the same
bounded policy. Before a dependency's metadata is read, its bytes, safe
locator, role, and digest are recorded. The bounded-input-set digest commits to
normalized declared resolution roots and deterministic ordered outcomes from
each assembly reference to one admitted candidate, ambiguity, or unresolved
state. The resolver does not probe the host GAC, runtime directories, SDK,
NuGet caches, working directory, or other implicit roots. Zero or multiple
admitted candidates fail closed with categorical gaps, so host installation
state cannot change facts or coverage under the same digest.

## Reader disagreement

Mono.Cecil is the primary convenience reader for the first slice.
`System.Reflection.Metadata` independently reads the admitted identity fields.
If the readers disagree, TraceMap emits a categorical disagreement gap and
withholds the disputed normalized fact so it cannot participate in later
reconciliation. There is no majority vote and no fallback to a display-string
match.

## First-slice fixture matrix

The default metadata-only fast matrix is targeted, not Cartesian:

| Shape | C# | VB.NET | F# | macOS fast | Windows |
| --- | :---: | :---: | :---: | :---: | :---: |
| Overloads; generic methods/types; nested/generated names | yes | yes | yes | required | required |
| Interfaces, explicit implementations, inheritance, overrides, virtual dispatch declarations | yes | yes | targeted compiled shape | required | required |
| Delegates, events, lambdas, async and iterator state machines | yes | yes | targeted compiled shape | required | required |
| `ref`/`out`/`in`, `ByRef`, arrays, pointers, function pointers where toolchains support them | yes | yes where representable | targeted | portable subset | full supported subset |
| Missing/unresolved/partial dependencies and malformed bounded inputs | yes | yes | yes | required | required |

The first slice asserts compiled metadata identities and explicit input,
provenance, dependency, reader, and bound gaps. It does not read IL method
bodies, reconcile source identities, inspect PDB sequence points, or require
ILAsm.

## Deferred validation matrix

These rows are later-slice contracts and are not part of the tasks 1-7 fast
suite:

| Shape | Language/input | macOS | Windows | Owning slice |
| --- | --- | --- | --- | --- |
| Source/compiled reconciliation and ambiguity | Independent source canonical-identity matrix plus C#, VB.NET, and F# compiled identities | later | later | task 8 |
| Portable PDB identity and sequence points | C#, VB.NET, F# | later | later | task 9 |
| Windows PDB identity and sequence points | Windows-produced C# and VB.NET inputs | explicit not-run | required later lane | task 9 |
| `call`, `callvirt`, constrained calls, `newobj`, `ldftn`, `ldvirtftn` | compiled/IL fixtures | later | ILAsm cross-check | task 10 |
| Exception handlers and unusual control flow | curated public IL cases | later read-only lane | ILAsm/runtime-safe checks | task 10 |
| Legacy .NET Framework/Web Forms build | Windows-produced inputs | explicit not-run | required later lane | task 11 |
| C++/CLI and mixed mode | separate feasibility inputs | unsupported | separate feasibility lane | task 11 |

Every promoted regression case, in its owning slice, records a stable case ID,
source construct, expected CLR shape, expected rules/tiers, expected gaps,
toolchain lane, and the single behavior it proves. A larger optional stress
corpus stays outside the default suite.

## `dotnetperf` use

The pinned historical corpus is an access-controlled validation source, not a
mandatory default-CI dependency or an oracle. Do not import its more than
15,000 tests wholesale. Mine its distinct exception-region,
branch/switch, `leave`, retargeting, nested/generic, duplicate-assembly, raw-IL,
netmodule, and PDB dimensions. Promote only minimized public reproductions when
licensing and privacy permit. Keep the full corpus in the later isolated
Windows endurance lane.

## Failure behavior

Missing, stale, ambiguous, unbound, or mismatched assembly evidence; unreadable
metadata; native/mixed-mode input; dependency failure; unsupported signature;
reader disagreement; budget exhaustion; or reconciliation ambiguity emits an
explicit rule-backed gap and reduces the bounded compiled coverage. It never
turns into a clean absence conclusion and never degrades already valid source
evidence.
