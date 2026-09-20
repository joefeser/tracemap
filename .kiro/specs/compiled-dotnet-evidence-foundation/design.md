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

Each compiled input record contains:

- safe input locator and input role;
- file SHA-256;
- bounded-input-set SHA-256;
- exact invoked generator SHA-256;
- extractor ID and version, including the pinned Mono.Cecil version;
- normalized assembly and module identity plus MVID when readable;
- source repository and commit only when a validated receipt binds them;
- build/toolchain identity when a validated receipt supplies it; and
- coverage state and categorical gaps.

The bounded-input-set digest uses deterministic ordered records of admitted
safe locators, roles, and file digests. A shareable projection computes its
digest after privacy projection and never republishes a private source digest.

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
parameter signatures, and readable custom modifiers. Metadata tokens are
locations within one module, not portable global identities. MVIDs distinguish
modules but do not bind them to source by themselves.

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
| Source/compiled reconciliation and ambiguity | C#, VB.NET, F# compiled identity with unsupported F# source gap | later | later | task 8 |
| Portable PDB identity and sequence points | C#, VB.NET, F# | later | later | task 9 |
| `call`, `callvirt`, constrained calls, `newobj`, `ldftn`, `ldvirtftn` | compiled/IL fixtures | later | ILAsm cross-check | task 10 |
| Exception handlers and unusual control flow | curated public IL cases | later read-only lane | ILAsm/runtime-safe checks | task 10 |
| Windows PDB and legacy .NET Framework/Web Forms build | Windows-produced inputs | explicit not-run | required later lane | task 11 |
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
