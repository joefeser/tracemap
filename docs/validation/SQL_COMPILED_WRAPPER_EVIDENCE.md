# Public compiled-wrapper SQL evidence

## Explicit retained compiled scalar transformation — compile-only, 2026-10-29

IL extractor 0.2.13 / policy `explicit-il-body-evidence.v17` adds
`--il-compiled-scalar-boundary`, requiring opaque root initialization and selected
roots. Default compiled-callee refusals remain. Only unique admitted/provenance-
bound, dual-reader raw/call/object-agreed static/nonvirtual/nongeneric String-to-
String callees qualify. Caller arguments must be scalar string temporaries/literals;
no tracked root/reference argument or unknown receiver is admitted.

Bounds are 128 instructions, 4 exception regions, 32 call observations, 32 locals,
stack 16 and 4 summaries/root, under existing aggregate work/chain/field/heap caps.
Instruction offsets, branch/switch targets and exception ranges are checked. Catch,
finally, filter and fault metadata are retained structurally. An over-approximate
bounded control graph must expose a return, not prove runtime reachability. Direct
field writes, explicit throws/rethrows, indirect calls/jumps, block/pointer stores,
field-address/delegate construction, incomplete calls/metadata and wrong signatures
refuse. Unknown or ambiguous body matches do not become opaque external calls.

This is a conditional scalar-transform boundary, not a purity or passthrough proof.
Internal branches, handlers and calls are NOT executed or semantically resolved.
No direct field-write opcode does not establish absence of transitive effects or
escapes. Normal return and noninterference with unpassed tracked heap are explicit
assumptions; possible throws and transitive effects remain unmodeled. The declared
String result is unbound, never assumed equal to its input or successfully decrypted.
Root constructor linearity/private scalar terminal-store checks still apply, and
SQL identifier/type/size/control consumption of unknown values still refuses.

Only after the complete caller terminal store qualifies are compiledCallees receipts
committed inside opaqueInitializers. They bind actual call/signature, target body/
binary/token, handler kinds and bounds to the containing generator/input provenance.
Policy is explicit-inspected-compiled-scalar/1; candidate schema remains the separate
il-sql-lifecycle-opaque-entry/1 conditional entry contract. Current read/comparison
routes and execution/allocation identities are unchanged. Ordinary compiled calls
outside this opt-in constructor boundary remain inspected/refused as before.

Independent public C#/VB fixtures combine a four-region branching string transform,
constructed-reference ownership, opaque configuration inputs, parameter attachment/
Fill and external trace. They use only public synthetic constants/APIs; no private
transform/decryption source or algorithm is copied. Negative definitions cover
writes/throws, SQL/control use, instance/generic/missing agreement and resource caps.
Native definitions check three flags, conditional schema and summary provenance.
All are compile-checked only: no tests, native scans, configuration/transform/provider/
application methods are executed. Real-input lineage/whole SQL completion is unproven.

## Opaque lookahead rejection attribution — compile-only, 2026-10-28

IL extractor 0.2.12 / policy `explicit-il-body-evidence.v16` fixes a diagnostic bug,
without relaxing admission. Opaque lookahead already allows eligible external calls
with NO matching admitted body. Its compiled-callee refusal is reached only when a
body/metadata alias DOES match. Previously a later rejected compiled helper could
be mislabeled with the earlier configuration getter where normal interpretation
first stopped. That message did not prove System.Configuration input was required.

The interpreter now preserves the actual rejected call/site/signature hash, chain
start and normal-interpretation trigger separately. Gap opaqueInitializerRejection
lists at most two exact matched body/binary/token bindings, input role/outcome,
retained/unavailable metadata/retention state, scalar return kind and call shape.
Bounded instruction/handler/call counts and opcode-level branch/field-write/throw
flags describe available bodies only; null flags mean unavailable metadata. They
are not purity, reachability or normal-return proofs. The gap names the actual
rejected signature hash; metadata stage is opaque-initializer-lookahead.

One retained match, one metadata-unavailable raw/alias match and multiple matches
have distinct reasons. Missing metadata and ambiguity remain refusals. The guard
against silently replacing available compiled behavior with an opaque assumption
is unchanged. Normal inspection of a fully interpretable helper still proceeds
when no earlier external unknown prevents ordinary interpretation. Pending lookahead
state is not reported as a rejection on such a successful path. Candidate schemas,
SQL/execution identity, all bounds and existing read/compare contracts are unchanged.

Independent public C#/VB fixtures use a standard external configuration/string chain
with no System.Configuration compiled input; another adds a small admitted scalar
helper later in that chain. Definitions cover external-boundary eligibility, exact
later-helper attribution, ordinary known-helper inspection, missing metadata and
ambiguous matches. They compile only; tests/configuration/provider calls/native
scans are not executed. No employer source or helper identity was copied into Git.
The work-machine prompt inspects existing evidence first and returns a short text
helper-shape capsule, not code/photos or an assumed framework-policy fix.

## Metadata-proved constructor field owners — compile-only, 2026-10-28

IL extractor 0.2.11 / policy `explicit-il-body-evidence.v15` retains each body's
exact direct BaseType independently through Cecil/SRM. It participates in object
metadata agreement and actual-text charging under unchanged retention/field/body
caps. No scope or assembly-version normalization is introduced.

A fresh derived allocation initially owns only its concrete type's slots. A direct
nonvirtual inspected base-constructor call may add one field-owner view only if:
both source and target object metadata agree independently, the caller is a
constructor whose this is the same tracked receiver, the caller owner is already
admitted on that allocation, and its declared direct BaseType equals the target
constructor owner exactly. Target provenance/shape checks remain. Same-class
constructor delegation adds no ownership. At most 24 owner views/allocation and 256
view records/run are allowed under existing call-depth/work/heap limits.

This supports a reference-field initializer constructing a derived custom object
whose inspected base constructor stores base-owned fields. It does not make
reference fields opaque. Concrete allocation identity is unchanged. Fully scoped
base/derived field keys remain separate, including same-named fields. Unrelated
constructor/field owners, missing/unagreed metadata and generic/virtual targets
remain unknown. Unmodeled calls inside a base constructor still refuse; root-only
opaque scalar admission and the trace boundary are not widened.

Optional constructorOwnerViews receipts bind call site, receiver allocation,
derived/base owner hashes and exact target body/binary. Residual field-owner gaps
include fieldOwnerFailure with executing/allocation owner hashes, field identity
hash, admitted owner hashes, receiver-is-this, constructor flag, field-declared-on-
executing-owner and stored value KIND. No value or raw source snippet is added.
The existing read/entry/conditional schemas and comparison routes remain separate.

The old work-machine diff is not available and has not been reviewed/restored.
This is an independently implemented, metadata-proved ownership case, not a claim
that reference-field type alone explains the private refusal. Public C#/VB fixtures
combine a constructed reference field, same-named base/derived fields, opaque root
initialization, SQL execution identity and external trace. Negative definitions
remove/change the declared base, remove reader agreement and inject unrelated
field slots. Tests are authored and compile-checked only; private matching and
behavior remain unproven. No arbitrary owner-check bypass is added.

## Exact external System.Web trace boundary — compile-only, 2026-10-28

IL extractor 0.2.10 / policy `explicit-il-body-evidence.v14` adds the explicit
`--il-external-trace-boundary` flag, requiring selected roots. It recognizes exact
signed neutral System.Web 4.0.0.0 (token b03f5f7f11d50a3a) MemberRef signatures for
static HttpContext.get_Current, instance HttpContext.get_Trace and instance
TraceContext.Write(String) or Write(String,String) with Void return. The entire
chain is recognized atomically; it is not a general getter/void-method allowlist.
No System.Web implementation or runtime environment is loaded/executed.

Only the selected producer/loader root body at depth 0 qualifies, with an empty
interpreter stack and no active modeled enumeration loop. Reference results cannot
be stored, duplicated, returned, branched on or passed outside the chain. Arguments
are scalar string literal/null values, root arguments, locals or own private field
loads with proved modeled string/opaque-string origins. No arbitrary argument-call
computation or tracked object argument is admitted. Other overloads, signing keys,
versions, owners or incomplete call metadata refuse. Context non-nullness is unknown.

Bounds are 8 chains/root and 16 encoded instructions/chain, under existing aggregate
work limits. No branch may enter the chain's middle, and exception-region membership
must remain unchanged across it. Receipts retain all three sites/signature hashes,
argument origins, possible throws/unmodeled effects and the innermost enclosing
catch site when present. Normal return and noninterference with the tracked data
heap are explicit assumptions, not findings that tracing is pure or effect-free.
Catch continuations remain unexecuted. Runtime context/logging behavior is unproven.

Used boundaries select `il-sql-lifecycle-opaque-entry/1` with optional externalTraces
receipts; existing strict read/comparison contracts are unchanged. Defaults retain
unknown-call refusals. Scalar initializer mode remains separately opt-in. Guarded
runs select their schema independently, preventing a conditional boundary on one
run from relabeling a different run that did not use it. SQL parameter allocation
and execution identities remain in the existing interpreter model.

Public C#/VB fixture and test definitions cover both Write overloads, scalar root/
local/opaque-field data, separate SQL executions, forged signatures, missing call
metadata, context branches/stores, tracked-object arguments, overload/chain limits,
legacy read-root refusal, explicit flag validation and native conditional schema.
Only compilation is checked; tests/native scans/provider/application/trace methods
are not executed under the standing no-tests instruction.

Work-machine reports based on dirty tool files are evidence for those exact mixed
binaries, not just the Git commit. The handoff preserves local changes and requests
actual generator/delta bindings privately plus a neutral public-tool delta summary.
Wrong outer façade roots are deselected; reference-field constructor admission is
not added. Full real-input lineage and public-release acceptance remain outstanding.

## Opt-in opaque root scalar initialization — compile-only, 2026-10-28

IL extractor 0.2.9 / policy `explicit-il-body-evidence.v13` adds
`--il-opaque-root-initializers`, requiring explicit selected roots. Default behavior
remains strict. Only a fresh producer/loader root's unique parameterless constructor
can abstract a linear external-call chain into its own private scalar field. Read
roots retain the strict constructor route. Existing available/metadata-failed
compiled callees are not blackboxed. Unknown effects are not treated as pure.

Admission requires no constructor branching or exception handlers, no intermediate
stores/escapes, no tracked reference arguments/receivers, and no void/byref/generic
calls. Arguments are chain temporaries or literals; instance receivers must be
chain temporaries. The terminal store must consume the root receiver and exactly
one opaque result whose agreed encoded return type equals the private field's
primitive String/Boolean/integer/floating/Char type. No arbitrary struct, Object or
DataSet field is admitted. A temporary reference is not claimed fresh or local.
Opaque fields can initialize only once and later explicit writes refuse.

Bounds are 8 fields/root, 32 instructions/chain, 8 calls/chain, 4 arguments/call and 16
chain stack values, plus existing interpreter/closure/heap/work bounds. No runtime,
configuration, provider, decryption or environment access is performed. Only the
already dual-reader-agreed retained instruction/call/field metadata is interpreted.
Opaque field values carry typed unknown origins. They may flow as data through
inspected wrappers/parameter Value assignments, but never become assumed command
text, parameter names/types/sizes, branch outcomes or tracked object identities.

Using the boundary emits a separate `il-sql-lifecycle-opaque-entry/1` entry schema,
not the existing column-read or comparison contract. Initializer receipts identify
store and call sites, signature/field hashes, declared types, possible throws and
unmodeled effects. Claims explicitly assume normal return and noninterference with
unpassed tracked heap state. They do not prove constructor completion, purity,
external non-mutation or absence of aliases. This is conditional partial evidence
under an opt-in abstraction, not independently established external-call behavior.
The selection flag/policy/bounds are bound into generator/input provenance. Later
refusals retain initializer receipts but discard executions/positive entry claims.

Independent public C#/VB compile-only fixtures include opaque producer data flow,
SQL/control/mutation refusals, root-reference arguments, non-scalar fields, handlers,
long chains and strict read roots. Tests also define missing field metadata/byref
refusals, explicit-opt-in validation and native separate-schema emission. Definitions
are authored/compiled, not executed. No private code or report text was added.

## Closed legacy core ArrayList profile — compile-only, 2026-10-28

IL extractor 0.2.8 / policy `explicit-il-body-evidence.v12` deliberately extends
lifecycle admission to exact signed neutral mscorlib 2.0.0.0 alongside 4.0.0.0 for
ArrayList parameterless construction, Add(Object), GetEnumerator, IEnumerator
Current/MoveNext, IDisposable.Dispose and RuntimeHelpers.GetObjectValue. Method
signatures, signing key, culture, namespace and type must match exactly. Enumerator
return scope matches its owner's selected core profile. Existing primitive/Object/
IDisposable type-operand checks admit either exact core identity; typed unknown
boxing still requires the matching declared root scalar type. The previously added
legacy Object base constructor remains neutral only on a tracked custom receiver.

No global version normalization or general legacy-framework support is added.
System.Data 2.0.0.0, legacy generic lists, alternate collection APIs/overloads,
mutation, escape, invalid elements, incomplete/repeated enumeration and invalid
attachment remain refusals. All size/work/heap/depth bounds remain. SQL read/entry
schemas and compare routes are unchanged; this is an intentional additive versioned
admission-profile change. The object-path catalog remains separate and unchanged.

C#/VB regression definitions rewrite only core AssemblyRefs of independently
public synthetic fixture bytes in memory, then require dual-reader agreement and
check constructor fields, boxing, enumeration, producer/consumer lineage, separate
executions and negative collection/attachment cases. Exact-identity controls keep
wrong versions, keys, cultures, namespaces and stack shapes outside admission.
Definitions are compile-checked only; no behavioral success is claimed. The prior
4.0-only refusal expectation now rejects 3.0.0.0 because 2.0.0.0 is intentionally
admitted for this closed protocol. Existing fixture source files are unchanged.

The work-machine prompt requires a bounded full framework-identity inventory for
the selected closure before one native run, so a later failure returns all known
remaining framework mismatches rather than one constructor at a time. A stub with
a throwing wrapper remains inadequate as complete replay proof. Private-input
lineage, owner comparison and closure of the ArrayList blocker remain unproven.

## Exact legacy Object base constructor — compile-only, 2026-10-28

IL extractor 0.2.7 / policy `explicit-il-body-evidence.v11` adds one identity to
the lifecycle framework catalog: signed neutral mscorlib 2.0.0.0 (token
b77a5c561934e089), System.Object, instance/non-generic/default-call/parameterless
.ctor returning Void. It invokes the existing neutral base-call action only on a
tracked custom object, then continues inspecting subsequent field stores. There
is no global assembly-version normalization or arbitrary constructor skipping.
The existing 4.0.0.0 base call, exact ArrayList/System.Data identities, object-path
route and SQL compare contracts remain unchanged. Legacy ArrayList identities
still refuse. Missing fields, unknown values and unsupported calls remain gaps.

New test definitions rewrite only the encoded base-constructor reference in the
independently authored public C#/VB InputParameter fixture, in memory, then require
Cecil/SRM agreement before checking end-to-end field/parameter/execution lineage.
Negative catalog definitions vary version, token, assembly, culture, type, instance
shape, arity and parameters. These tests were authored and compile-checked, not
executed. No application or SQL execution, private input publication, native replay
or ArrayList acceptance claim is included.

## Root-sliced retention — compile-only, 2026-10-28

IL extractor 0.2.6 / policy `explicit-il-body-evidence.v10` adds repeatable
`--il-lifecycle-root <binary-sha256>:<MethodDef-token>` (max 16; requires explicit
`--il-body-evidence`). The root must uniquely match an admitted raw body. Default
corpus mode is retained. Selected mode defers corpus object metadata and skips
corpus-wide object-path/lifecycle interpretation; it reports partial coverage.
`ManagedIlBodyDeclared.objectBodyScope: deferred-to-root-slice` is not a selected
metadata failure. Selected lifecycle candidates/gaps carry `lifecycleSlice`, schema
`il-sql-lifecycle-slice/1`, with generator/bounded-input hashes and member receipts.

Each sequential root lease retains only the root, its inspected parameterless
constructor when needed, and exact resolved call closure. Bounds are 128 bodies/root,
4,096 admitted raw bodies indexed, 4,096 reference lookups/root, 64 MiB verified
compiled input bytes/root, and at most 1 MiB characters per reader/root. The decoder
has one aggregate configured work budget across selections, separate from the
existing interpreter budget. The lease is disposed before advancing to another root.
The previous default corpus quota remains shared across inputs per reader.

Selected bytes are SHA-verified again. Both independent decoders must agree and
match the admitted raw body. Cecil metadata lookup locates candidate definitions;
it cannot establish a positive body without SRM agreement. Exact framed assembly
and member identities select cross-assembly candidates. Missing, changed, ambiguous,
over-limit or unagreed bodies remain explicit gaps. Existing instruction/field,
interpreter depth, heap, collection and execution-identity refusal gates remain.
Read/entry JSON contracts and compare routes are unchanged; this is an additive
opt-in analysis scope and receipt, not a new result-schema acceptance route.

The C#/VB CLI test definitions now cover selected roots, excluded unrelated noise,
retained member receipts and existing positive/negative entry controls. Selector
format, MethodDef identity, root-count and explicit-admission controls are authored.
Tests/native scans were not run at Joe's request. Compilation does not establish
private-input success or close the ArrayList blocker. The earlier scale fixture
was not a complete reproduction of actual-cost corpus starvation; no successful
large-corpus behavioral replay is claimed here.

## Metadata handoff repair — compile-only, 2026-10-28

IL extractor 0.2.5 / policy `explicit-il-body-evidence.v9` separates generic
value-summary agreement from structured object-body agreement. Object retention
still requires matching raw IL/call references, call stack shapes and independently
present/equal object metadata. Raw, call or object disagreements still refuse.
The shared per-reader 1 MiB character bound, 256-instruction and 64-field limits are
unchanged. Actual retained text is charged instead of worst-case maximum-width
reservations for every declared field per method.

Body facts now expose `il-object-body-retention/1` state/retention/agreement properties;
lifecycle gaps can include `bodyMetadataFailure` for root metadata, callee metadata
or constructor selection. Budget/text/field/instruction limits and peer disagreement
are distinguished. `il-complete` describes the raw body lane; it does not establish
that structured metadata exists or instructions were interpreted. Provenance
`unbound` is not an argument-origin or metadata-retention verdict.

The CLI and scale-shaped C#/VB fixture projects compile with warnings as errors.
Tests/native scans were explicitly not run. New definitions include 160 unrelated
methods before a producer and agreement/exhaustion controls. Historical pass totals
do not validate this patch. A failure-only sanitized diagnostic/reproducer request
is included in the work-machine prompt. The cap can still exhaust; no success or
private-application acceptance is promised from compilation.

## Entry-protocol patch — unvalidated behavior, 2026-10-28

A separate `ManagedIlSqlLifecycleEntryCandidate` / `il-sql-lifecycle-entry/1` contract
now admits exact signed DataSet-returning producers and SQL-related Void loaders.
Static roots or fresh inspected instances may have up to 16 unbound arguments;
unknown values are not replaced with defaults. Single-Boolean roots enumerate both
alternatives. Unknown required scalars/control, missing bodies, invalid initialization
and unsupported output shapes emit gaps. SQL-related entry rejection is now visible.

Entry facts retain returned/stored datasets, table/read lineage and execution-specific
parameter allocations. Value setters preserve opaque input origins before attachment.
A Void method needs observable stored SQL dataset state or a tracked read; a producer
needs a SQL-derived returned DataSet. Runtime values, ambient/session state, arbitrary
DataSet subclasses, predicates and catch/fallback completion remain unproven.
The old returned-column candidate and comparison contracts are unchanged; entry facts
cannot be bound as column-read evidence by `compare` or `compare-owner`.

**Tests were explicitly skipped at Joe's request.** New C#/VB fixtures and test
source are included for later execution. A warnings-as-errors CLI build passed;
that is compilation only. Historical test pass totals below do not validate this
patch. See the latest spec implementation-state section and work-machine prompt.

## End-to-end lifecycle route (public synthetic acceptance verified)

IL extractor 0.2.4 adds the separate `ManagedIlSqlLifecycleCandidate` contract
(`il-sql-lifecycle/1`, rule `dotnet.compiled.il-sql-lifecycle.v1`, Tier3). Earlier
object-path routes retain their refusals and independent 20/26-root denominators.
This route follows separately compiled wrapper bodies, two SQL executions, named
Merge destinations held through a property, application-authored auxiliary schema,
an inspected guarded overlay helper, raw reads and typed projections, Clear/re-Merge
and reload. Boolean arguments produce both correlated alternatives, never a guessed
runtime branch. Loops summarize existing rows under normal completion, not row counts.

`sqlserver-compiled-lifecycle compare` accepts independent synthetic result JSON
declarations/export, persisted scan facts, repo/revision, and
`sqlserver-compiled-lifecycle-bindings/1`. Each binding selects an exact candidate
digest, consumer table/column/read body/binary/offset/invocation, and every execution's
command plus four-part schema identity. Source-reader bindings are not accepted.
Command syntax is available through CLI help. Publication requires a new directory
and produces `compiled-lifecycle-comparison.json` and `report.md`.

Reports retain generator and bounded-input hashes, schema provenance, body links in
the selected scan facts, rule/tier and partial coverage. Schema match/mismatch/unknown
is separate from current value origin and source-column hash. An application overlay
does not become a SQL value; a column retained after Clear but absent from the new
Merge result has unknown current origin. Definite cleared-table reads, duplicate
table names, escapes, cross-row dependencies and loop-carried field effects refuse
positive lineage. Types, nullability, row existence and actual values are not compared.

Potential failure records include table and object/list before/normal-completion
snapshots and possible earlier-row/partial-update flags. They are not observed failures,
do not execute catch continuations, and do not prove rollback or final post-catch state.

The independently authored acceptance matrix lives in the spec's
`end-to-end-acceptance.md`. Actual CLI scan and comparison tests exercise both languages,
12 selected alternatives each (11 schema matches/one mismatch, two unknown current
origins), six refusal controls, tamper rejection and byte-identical replay. The frozen
full and retained smoke gates below verify this public synthetic scope.
No private acceptance, framework/session execution, SQL execution or migration claim.

The separate `sqlserver-compiled-lifecycle produce-owner-schema` and `compare-owner`
commands also admit explicitly owner-declared first-result shapes under
`database.sqlserver.owner-declared-result-shape.v1` (Tier3). Declaration/export
versions are `sqlserver-owner-result-declarations/1` and
`sqlserver-owner-result-schema/1`, with origin `owner-declared-result-shape`.
The comparison report uses `sqlserver-owner-compiled-lifecycle-comparison/1` and
retains `schemaOrigin`. Admission checks exact generator/input integrity and
repo/revision; it does not authenticate the owner, extract general DDL, verify
completeness independently, or establish deployed metadata. Keep private owner
artifacts/reports local. Existing synthetic admission rejects this origin.
Incomplete shapes stay unknown; declared matches/mismatches are conditional.
The inherited owner implementation and its tests were included in both recorded
gates below. The consolidated work-machine prompt documents the distinct routes.

### Bounded ArrayList continuation — 2026-10-28

The lifecycle route now inspects custom SQL parameter objects stored in an exact
signed net48 `System.Collections.ArrayList`. Supported operations are parameterless
construction, `Add(Object)`, `GetEnumerator`, and exact `IEnumerator.Current` /
`MoveNext`. At most 16 inspected custom objects are enumerated once, to completion.
Custom constructor stores and inspected field/property loads must resolve every
name, SQL type and size supplied to actual `SqlParameter` construction and command
attachment. Elements freeze on Add. Parameter allocations attach once; command
allocations fill once, with at most 16 parameters. Fresh collections/commands retain
distinct execution and returned-table identities.

Unknown element types, other collection APIs, mutation, unknown escape, incomplete
or repeated enumeration, missing/ambiguous/untrusted bodies and invalid attachment
withhold positive lineage and emit explicit gaps. Existing call-depth, instruction,
aggregate-work and heap bounds remain. No cap, previous refusal or original 20/26-root
matrix expectation was relaxed. The schema and other SQL routes remain unchanged;
this does not add ArrayList parameter-to-contract comparison to another route.

Compile-only public C#/VB fixtures cover field/property positives, separate
executions and ten negative roots per language. CLI tests assert all three positives,
all ten gaps, exact body links, privacy projection and byte-identical replay. Test
classes separate ArrayList roots from unrelated field-heavy lifecycle state so
bounded metadata retention remains available; mutable controls use a distinct type
without changing the legacy read-only parameter fixture.

The current uncommitted working tree (base `e24387ffb`) passed:

| Gate | Result |
| --- | --- |
| Focused lifecycle/owner/object-path/shape regression, `-warnaserror` | 87 passed, zero failures/skips |
| Full Debug solution, `-warnaserror` | 5,527 passed, zero failures, three existing skips; 46.0551 minutes |
| Original/named compiled native smoke | Unchanged 20 roots (10 candidates/10 unknowns), 26 named roots (4/22), two local shapes |
| Python compiled-wrapper guards | Five passed |
| Whitespace and preservation | `git diff --check` passed; five inherited owner files byte-identical to the recovery snapshot |

Exact commands, hashes, changed files, intermediate failures and remaining gaps are
recorded in the spec's `implementation-state.md`. Pinned source OSS smokes are
explicitly deferred for this compiled-only slice; no source-adapter code changes,
and those repositories contain no SQL calls. The historical frozen validation
below belongs to its stated earlier head. No SQL/provider
execution, private acceptance, Select predicate, catch/fallback execution, deployment
or general collection support is claimed.

### Earlier frozen validation — 2026-10-28

Implementation head: `7715833f9db6c6e4fd326dbec3013e3c5be8c0c5`. Source, HEAD and
CLI/Core/Reporting binaries stayed unchanged through the full suite and sequential
smokes. Subsequent closeout changes are documentation only.

| Gate | Result |
| --- | --- |
| Focused lifecycle/object-path/lineage, Debug `-warnaserror` | 52 passed, zero failures/skips |
| Full solution, Debug `-warnaserror` | 5,498 passed, zero failures, three expected skips; 30m 56s |
| Actual CLI lifecycle scan + compare, C# and VB | 12 alternatives each; 11 schema matches/one mismatch; two explicitly unknown current-value origins; six refusal roots per language |
| Original compiled native matrix, repeated twice | 20 roots: 10 candidates/10 unknowns; two local shapes; persisted C#/VB facts byte-identical |
| Named-table native matrix | 26 roots: four candidates/22 unknowns, unchanged |
| JSON and DDL parameter smokes | Each 8 matches/12 mismatches/32 unknowns |
| Result-column smoke | JSON 36/10/36; DDL 36/10/58; 20 refusals |
| Table-result smoke | 44/10/88; 24 refusals |
| DataTable smoke | 108/24/176; 22 refusals |
| DataSet smoke | 156/24/176; 24 refusals |
| Private-helper smoke | 84/30/116; 24 refusals |
| Internal-helper smoke | 72/24/60; 24 refusals |
| Field-read smoke | 78/36/42; 52 refusals |
| Python smoke guards | Compiled-wrapper five passed; SQL smoke guards 59 passed |
| Pinned C#/VB compatibility scans | 12,949 / 114,806 facts; artifact validators passed; reduced coverage |

Smoke triples above are match/mismatch/unknown. The three full-suite skips are the
existing full generated golden sweep, HTTP provenance interop check and deep
projectless Windows ASP.NET publish check. No full-suite failure or rerun occurred
at this frozen head. Earlier development failures and corrections remain in the
spec implementation history; they were not erased or converted to passing expectations.

Pinned compatibility revisions: C# `2a8e72c8f5680edf2096b05ac08c39d47a95cef8`,
VB `20d2a51dfc9f342848ad134952ceaa8d79302559`. Both repositories have no SQL calls;
these are adapter compatibility checks, not SQL coverage or application acceptance.

Frozen SHA-256s, verified unchanged after retained and pinned smokes:

- CLI: `e41cfa0fe312cb8f91eb930d0ebb3ead56ac6a08d10e554daa51ac5bc369f964`
- Core: `741fb6761d93b00b6147e62e02100ae7542c9660b5e485aca0a03006c6bc7162`
- Reporting: `f34cfcb1e01b73ed66b8ac334835740cb2026dc116f96ead94788e4d9ef3be4d`

The successful public flow is positively linked, not accepted merely as unknown.
Schema ancestry and current-value origin are independently checked: overlay values
remain application-origin, and a retained column absent from a later Merge result
does not acquire false SQL attribution. Failure records preserve possible partial
table/list state without claiming executed catch continuations or rollback.

The single later handoff is `prompts/continue-sql-compiled-wrapper-evidence.md`.
It checks retained input fit before any new scan and stops at known unsupported
private shapes or unadmitted production schemas. The comparison route's synthetic
JSON admission is not owner-DDL/catalog admission. No runtime SQL, container,
private-input, application-build, deployment, session/framework or migration
acceptance is established by these gates.

This is a finite compile-only corpus and a **partial** native evidence addition,
not completed SQL-A acceptance. No SQL, provider methods, containers or private
application inputs execute. Source fixtures are not proof of deployed binaries.

## Current object-path continuation

The independent `dotnet.compiled.il-sql-object-path.v1` route connects the authored
finite corpus across caller and separately compiled wrapper: **10 bounded static
candidates and 10 unknowns**, plus the unchanged two local shape facts. C# and VB
each support Direct, Forwarded, FieldObject, PropertyObject and MergeFresh.
Mutation, escape, conditional parameter flow, existing Merge destination and
post-Merge mutation remain refused.

Custom constructor stores and getter loads use dual-reader-agreed IL metadata,
fresh allocation identities and positional invocation frames. Every retained site
binds binary hash, body fact, IL offset and invocation. A unique admitted
binary/body is required; competing bytes for an assembly reference, missing
bodies, recursion, virtual/generic bodies, exceptional/conditional flow, unknown
calls and exhausted limits are unknown. This is a closed static protocol, not a
general heap engine or executed provider; encoded provider names are unauthenticated.

The consumer names a constant hashed column on first table/row. An optional
private destination field must be freshly assigned in the root invocation. Merge
records distinct filled and consumer allocations with
`fresh-empty-schema-union-candidate`, never original-result aliasing, schema
comparison, value/row/type/nullability or runtime-ownership proof.

IL version is `0.2.3`; source versions and legacy routes are unchanged. New bounds:
4096 indexed bodies, 256 instructions/body, 64 fields, 2048 steps, depth 16,
64 allocations, 256 sites, 16 parameters and 64 stack/local slots. Retained object
metadata has a separate per-reader character budget capped at 1048576 and
MaxTotalWorkUnits; interpretation has its own aggregate MaxTotalWorkUnits budget.
Bounds participate in the native bounded-input digest.
Allocation and execution IDs are invocation-path-local: bind them together with
the root body fact and the binary/body/offset/frame site tuple. Never join a bare
frame/allocation string across different root facts.

### Remaining acceptance requirements

The three compiled-flow legs are implemented for this corpus, not arbitrary
private wrappers. Actual acceptance additionally requires:

1. Supported result-schema admission and explicit server/database/schema/procedure
   identity. Complex bodies with locals, joins, computed projections and ordering
   remain outside existing DDL admission. This is a separate capability gap, not
   missing paths or permission to implement a general parser.
2. Exact private binary/source selection and owner-supplied identity, then a
   bounded fit check. Public static candidates are not deployed equivalence.
3. An independent admitted contract and comparison bound to these exact execution
   and consumer candidates before declaring parameter/column matches. Manual
   declarations must retain their real origin, never synthetic-independent labels.
   Case-only names remain unknown under existing contracts.

Do not rerun the work-machine loop simply to rediscover known contract-admission
and comparison blockers. No SQL, containers or private inputs run in this package.

### Bounded named-table continuation

The separate TableFlow matrix has 13 roots per language: First and Second are
positive static candidates; Conditional, Mutated, UnknownHelper, RepeatedMerge,
ClearMerge, Existing, Exceptional, DuplicateName, OutOfRange, DynamicSlot and
ThirdTable remain unknown. This adds four candidates and 22 unknowns without
changing the original 20-root matrix or its 10/10 result.

An inspected property setter can store a fresh DataSet into its exact private
backing field. At most two distinct named tables may be added, each receiving one
Merge from the first table of an inspected returned DataSet. Destination ordinals
must select an existing authored slot; source table and row ordinals remain zero.
Each consumer retains its selected execution and command/parameter evidence,
distinct filled/consumer DataSets, and optional `filledTableIdentity` and
`consumerTableIdentity`. These optional properties occur only on the new
`fresh-empty-table-schema-union-candidate` relation. Schema v1 is additive; the
local smoke matrix is version 3 with separate `rows` and `tableRows` denominators.

The entire inspected root must be supported, including effects after a read.
Unknown helper calls, value writes, Clear/re-Merge, conditional and exceptional
alternatives refuse the path. This conservative boundary prevents stale value
lineage; it does not compute replacement values or partial exception states.
The in-memory controls below characterize those distinctions separately.

### Broader application-shape fit requirements (not implemented)

Additional shape evidence arrived after the implementation freeze. It does not
change the validated 20-root denominator and is not exact executable source:

- A holder property receives a fresh DataSet and two named tables. The first
  result table of two **different DataSet-returning functions** is merged into
  destination tables zero and one. Keep two execution lineages; neither repeated
  fetch identity nor snapshot-copy equivalence is established.
- The bounded fresh property/two-table/DataTable.Merge/index-one form is covered
  only by the separate TableFlow matrix above. Existing state and unknown aliases
  remain unsupported; no general property or session lifetime analysis is implied.
- Data access constructs four custom parameter objects in an ArrayList and has
  open/execute, catch/logging, finally/close and a fallback DataSet. Exceptional
  alternatives, effects and collection propagation need separately bounded proof.
- Helpers intervene before consumption. One visible pattern uses a literal
  DataTable.Select predicate, then a dynamic key-based Select and guarded row
  value assignments. Retain predicate reads, RHS column reads and LHS writes
  separately. Value writes alone do not prove schema mutation or preserve value
  lineage; unknown helper effects and holder aliases remain unknown.
- An auxiliary DataSet is application-authored: a named table with seven typed
  Columns.Add declarations, distinct keys, derived counter values and Rows.Add.
  It is not a stored-procedure result schema. Refresh clears class state then
  assigns a builder result. One observed save caller does not prove save-only
  initialization, caller completeness or state availability; commented code is
  not initialization evidence.

These require independently authored public C#/VB fit fixtures before widening
analysis. Preserve supported/unsupported legs and initialization/value/schema
distinctions; never copy private code or turn an OCR shape into exact source.

#### Required correction controls (behavior controls, not positive extraction)

The next synthetic matrix must distinguish three independent identities:
destination allocation, merged column/schema origin, and value origin at a read.
Creating an empty table locally does not erase the SQL origin of columns admitted
through Merge. A conditional value assignment does not prove schema mutation.

Pin these cases independently in both languages before widening evidence:

| Case | Required bounded conclusion |
| --- | --- |
| Enrichment guard passes and key matches | Only the assigned cells have replacement-value evidence. |
| Guard fails, key misses, or no rows qualify | No overwrite is established; incoming values may remain. |
| A later read follows an unknown helper | Current value origin remains unknown. |
| Failure before destination reset | Prior state may remain; do not assert fresh-empty state. |
| Failure after allocation or either merge | Record that boundary's reachable state, not a blanket empty result. |
| Failure during enrichment or list population | Partial mutation/population is possible. |
| Dependency throws into an enclosing constructor catch | Later initialization is not assumed to execute. |
| Session-held object is explicitly replaced | Session lifetime does not establish immutable object identity. |

Keep these as public synthetic requirements, not confirmed private defects or
runtime observations. Existing success/unknown denominators remain unchanged.

#### In-memory behavior controls

`DataSetLifecycleBoundaryTests` now pins 12 synthetic .NET 10 cases: guarded and
unmatched updates, column import into a locally allocated table, Clear/re-Merge
value replacement, seven explicit injected-failure boundaries, and an enclosing
catch that skips later initialization. The failure matrix distinguishes prior
state, fresh allocation, first/second merge, partial enrichment and partial list
population. It injects failures *between* operations; it does not characterize
atomicity or rollback when Merge itself throws.

Focused Debug test run with `-warnaserror`: 12 passed, zero failed/skipped.
These tests execute only System.Data in memory, without a provider or database.
They establish fixture behavior, not extractor correctness, C#/VB parity, private
application behavior or new contract admission. No production defect was shown.
The original compiled-wrapper matrix remains unchanged; the new TableFlow matrix
is reported separately. Conditional/helper/exception cases are extraction refusals.

### Continuation validation

Frozen implementation `48ae738fb75062eb55af3b87eae3e9b5921cb871`:

- Full Debug `-warnaserror`: **5,443 passed, zero failed, three existing skips**,
  33m34s, exit 0, no warnings. No full-suite retry.
- Focused IL/command/SQL/Razor lane: **193 passed, one existing Windows-only skip**.
- Native matrix twice: 20 roots, two local shapes, 10 static object paths and 10
  unknowns; C# and VB facts byte-identical between runs.
- Nine retained offline smokes unchanged: parameter/DDL 8/12/32; result JSON
  36/10/36, DDL 36/10/58; table 44/10/88; DataTable 108/24/176; DataSet 156/24/176;
  helper 84/30/116; internal helper 72/24/60; Field 78/36/42 with 52 refusals.
- Pinned C#/VB: 12,949/114,806 facts, reduced coverage, FailedOrPartial application
  builds and zero SQL calls. Artifact validators pass; no application/SQL proof.
- Four Python guards, private-path and whitespace checks passed.

Development matrix initially failed C# Direct on the inherited DbCommand setter
signature (VB passed); a diagnostic repeat confirmed the same failure. The exact
encoded signature was added, with no expectation changes, before final gates.
CLI/Core/Reporting SHA-256 stayed unchanged across final gates:

```text
CLI       600837d659efa1322a3190dcf08780dbdd5641870d7a948b77bba2c42003e9e5
Core      64caae16b3c47225aecededf5f7708b115c36e495f2271d50e310880e402a2a5
Reporting ea79e1ca7124aacf3c669fffe194684e2ba5b5e08031d09a7673656888e1617d
```

Results/fit-note documentation follows the frozen implementation. The broader
96-output framework sweep, non-.NET, Windows, runtime and private acceptance
remain deferred. No SQL, container or fixture method was executed.

## Foundation denominator and implementation (historical)

Two separate assemblies per language: a C#/VB caller and wrapper, built against
.NET Framework 4.8 reference assemblies. Ten independently authored entry cases
per language cover direct local construction, incoming parameters, custom field
and property forwarding, mutation, escape, branches and three DataSet Merge cases.
The spec's design matrix was committed before extractor implementation.

`dotnet.compiled.il-sql-shape.v1` emits a new sibling
`ManagedIlSqlShapeCandidate` at **Tier3SyntaxOrTextual**. It admits only a fresh
local command/adapter, a constant-name/type/size SqlParameter constructor, exact
typed attachment, one Fill into a fresh DataSet and that allocation's single
return operand. The constructor, attachment, Fill and body retain exact native
fact links and generator/bounded-input/binary hashes. Names remain UTF-16 hashes.
Encoded framework names are not authentication or runtime semantics.

The IL extractor is `0.2.1`; source C#/VB extractors remain `0.22.23`/`0.10.26`.
Legacy command facts, parameter-unavailable gaps and comparison contracts remain
unchanged. No existing source SQL bindings accept the new candidate as a match.
Bounds: 256 calls/body, 16 parameters/body, independent aggregate derivation work
equal to the existing IL policy limit. Unsupported control flow, effects, scope,
incoming parameters and returns produce unknowns. Generic unmodeled-call effects
remain visible even on the admitted encoded shape.

## Reproduction

Build the normal test project (it builds both compile-only fixture pairs), then:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror --filter 'FullyQualifiedName~IlSqlShapeExtractorTests|FullyQualifiedName~IlSqlObjectPathExtractorTests'
python3 -m unittest discover -s scripts/validation -p test_sql_compiled_wrapper_smoke.py
python3 scripts/validation/sql-compiled-wrapper-smoke.py --cli src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll --out /tmp/public-compiled-wrapper-new-run
```

The output directory must not exist. The native scans and local-only matrix retain
input and generator hashes. Current expectation: **20 entries, two local shapes,
10 bounded static object paths and 10 unknowns**. A shape reached by multiple entries is not counted as
multiple successful bindings. Whole-path status cannot become clean merely because
the smoke's expected unknowns reproduce.

## Foundation remaining requirements (historical; superseded above)

1. Constructor-to-field/property forwarding needs actual compiled heap-store/load
   evidence and exact caller/callee identity, not a wrapper-name exemption.
2. Incoming parameter and returned DataSet substitution need provenance-bound
   interprocedural object identity and effect/alias controls. Current local shape
   evidence does not link a caller consumer to an admitted SQL result contract.
3. Merge into a field-held destination requires conservative schema union,
   pre-existing destination state and mutation/escape evidence. A freshly allocated
   destination alone does not make it identical to the incoming result.
4. Actual schema/server/procedure identity and supported SQL contract admission
   remain external acceptance inputs. General joins, ORM expansion and runtime
   execution are not implemented by this package.

These are explicitly unimplemented legs, not requested work-machine retries.
Do not ask the operator to rerun this corpus to resolve them.

## Validation record

Development: both fixture pairs built without warnings. Initial four tests failed
because the prototype rejected the generic call-effects gap; the implementation
now preserves it while admitting only recognized encoded calls. The next four
passed. Existing IL lane: 172 passed, one existing Windows-only skip. A later native
test build correctly failed `-warnaserror` on xUnit2031 despite ten passing tests;
the assertion was repaired, then 12 focused tests passed. The development native
replay produced the exact 20/2/20 denominator. The expanded IL lane passed 178 tests
with one existing Windows-only skip; the final narrow shape/Razor lane passed 26.

Frozen implementation: `fa347703efa4b535b2617feec5b9dadc443da4dc`.

- Full Debug `-warnaserror`: **5,433 passed, zero failed, three existing skips**;
  36m11s, exit 0, no warnings. No full-suite retry.
- Native replay ran twice: **20 entries, two local shapes, 20 whole-path unknowns**.
  C# and VB `facts.ndjson` files were byte-identical between runs.
- Nine retained offline SQL smokes passed with unchanged match/mismatch/unknown
  counts: parameter and DDL 8/12/32 each; result JSON 36/10/36 and DDL 36/10/58;
  table 44/10/88; DataTable 108/24/176; DataSet 156/24/176; helper 84/30/116;
  internal helper 72/24/60; Field 78/36/42. Refusal expectations were unchanged,
  including the corrected 52 Field-read refusals.
- Pinned public C#/VB compatibility smoke: 12,949 / 114,806 facts, both reduced
  coverage with failed/partial application build status. Neither contains SQL
  calls. This verifies scanner artifact compatibility, not those applications'
  builds or compiled SQL coverage.
- Four Python smoke guards, private-path guard and whitespace checks passed.

CLI/Core/Reporting bytes were frozen throughout (SHA-256):

```text
CLI       0b1222ca93b59d36dddfa85d76a31a16fbdee54e1aa7ae2ba59917c2ebcc6984
Core      c2d2185288d3b8c2c5ada6ce97926c7b349916126b5850c91f3e19e4c9e565cd
Reporting 378bb1f0713752435af56437c1c1ec31f9edb18592172555488e2fa6b5eaeaba
```

No SQL Server, container, fixture method, private application or Windows runtime
execution occurred. The broader 96-output multi-framework CLI sweep and non-.NET
lanes were not rerun; coverage here is the full .NET suite, existing IL lane,
new actual C#/VB compiled corpus, retained SQL smokes and pinned source adapters.
Results documentation follows the frozen implementation in a docs-only commit.
The consolidated continuation is `prompts/continue-sql-compiled-wrapper-evidence.md`;
it is a local capability prompt, **not** a work-machine acceptance retry.

## Named-table continuation: frozen validation

Implementation head: `7b06f08e6920adf0f2a4d5e5138f79e62bbd06a3`.
These results supersede pending validation for the bounded named-table slice,
not the historical gates above. IL extractor: `0.2.3`.

| Gate | Result |
| --- | --- |
| Focused extraction/lifecycle | 28 passed |
| Broader IL lane | 206 passed, one skip, zero failures |
| Full Debug `-warnaserror` | 5,461 passed, three existing skips, zero failures, no warnings; 43m16s; no retry |
| Native smoke, twice | Original 20 roots: 10 paths/10 unknowns; new 26 roots: four paths/22 unknowns |
| Deterministic replay | C# and VB facts byte-identical between runs |
| Parameter / DDL parameter | Each 52 cases: 8 match / 12 mismatch / 32 unknown |
| Result column | JSON 36/10/36; DDL 36/10/58; 20 refusals |
| Table result | 44/10/88; 24 refusals |
| DataTable | 108/24/176; 22 refusals |
| DataSet | 156/24/176; 24 refusals |
| Private helper | 84/30/116; 24 refusals |
| Internal helper | 72/24/60; 24 refusals |
| Field reads | 78/36/42; 52 refusals |
| Python guards | Five compiled-wrapper and 59 SQL Server guards passed |
| Pinned public C#/VB scans | 12,949 / 114,806 facts; artifact validator passed for both |

Triplets are match/mismatch/unknown. All nine retained SQL smoke denominators
remain unchanged. Full suite preceded sequential committed-head smokes. Both
pinned source scans retain reduced coverage and FailedOrPartial application
build status; neither contains SQL calls, so they establish no SQL coverage.

One broad Python discovery command produced 32 setup errors in the unrelated
argument-driven SQL command-text harness (`CONFIG` absent); it bypassed required
`--tool`, `--paths`, and `--assembly` initialization. This invocation is not counted
as a passing gate. Correctly scoped guard commands passed as reported above.

Frozen binaries, unchanged before and after regression and pinned smokes:

```text
CLI       7aa8a79b94b5c51d8f2ab18184e404d0210d353f7a77a34c782174f0ccd03ff3
Core      021bb9912422167da2ca7e4e7da81a675fec50871f79728234b5d1b8096bbe3b
Reporting c096e73cec7ee38083e0ed72a244b7ee4c60e93deff757a68194959eea3a7580
```

Conditional writes, unknown helper effects, exceptional paths and destructive
re-Merge remain extraction refusals. Twelve in-memory lifecycle controls describe
behavior without upgrading it to extraction support. No private application,
SQL Server, container or Windows runtime was executed. The broader multi-framework
sweep and non-.NET lanes remain deferred for this bounded IL change. This is not
private-application, SQL-contract, runtime or migration acceptance. The consolidated
next-step prompt remains `prompts/continue-sql-compiled-wrapper-evidence.md`.

## Computed external trace arguments — 2026-10-29

Extractor 0.2.14 / evidence policy v18 / external trace policy 2 adds exact signed
framework scalar string computations to selected producer/loader roots under the
existing external-trace flag. Concat/Format, selected primitive conversions and
String instance operations yield unbound String values with call/origin receipts.
Only known scalar operands qualify; Object formatting of tracked/unproved objects,
arbitrary argument helper calls, computed SQL names and default/read routes retain
refusals. This does not skip arbitrary argument evaluation or prove Write pure.

Partial trace receipts mark computed arguments and retain mayThrow, normal-return
and external-effect assumptions. The versioned trace bound is64 instructions/chain
(previously 16),16 expression stack items, 16 array elements, 32 computations/run and
8 chains/root under existing work/heap bounds. Internal trace arrays are fresh;
preexisting array mutation inside a trace chain refuses. Catch continuations remain
unexecuted. Exception.Message on an unproved receiver remains unsupported.

Public C#/VB positive and refusal test definitions were authored. Compile-only
CLI, C# fixture, VB fixture and test-project builds passed with -warnaserror and
zero warnings/errors. Tests and scans were NOT run, explicitly deferred by Joe.
The private entry candidate and ArrayList blocker closure remain unproven.

## Field-access refusal diagnostics — 2026-10-29

Extractor 0.2.18 (skipping locally reported unpublished version numbers) / evidence
policy v19 records the pending field operation before receiver/custom-owner checks.
Optional fieldAccessFailure retains exact body/binary/invocation/offset/token,
opcode, receiver abstract kind/allocation/heap kind, owner/field hashes, declared
field type, nullable owner admission, this/constructor status, store-value kind,
reader agreement and at most seven neighboring opcode names. No operand snippets
or values are added. Opcode context is not full dataflow provenance. The receipt
is cleared after successful field operations and retained when that operation
refuses. Missing heap/type data stays null. Existing gap names and admission,
loads/stores, allocation/execution identity and bounds are unchanged; no bypass.
Existing fact generator/input provenance also binds this nested diagnostic.

C#/VB regression definitions cover unknown receiver refusal and custom owner
mismatch diagnostics. Tests/scans remain deferred by Joe. The work-machine prompt
permits one isolated clean-head build and at most one native scan, then a hard
stop; existing dirty patches/outputs must be preserved. Native lineage and the
ArrayList blocker remain unproven.

## Logging-only stack-name expressions — 2026-10-29

Extractor 0.2.19 / evidence policy v20 / external trace policy3 adds scalar and
fresh trace-array local stores plus a closed exact signed mscorlib 2/4 sequence:
StackTrace parameterless constructor, GetFrame(Int32), StackFrame.GetMethod and
MemberInfo.Name. Intermediate stack/frame/method markers remain expression-only
and framework-version-consistent; local storage of these references, arbitrary
calls, general reflection and dynamic/out-of-range frame indices refuse. Names
are unbound computed strings, not actual runtime identities. All four call sites
are retained with signature hashes and mayThrow/unmodeled receipts. Normal return,
non-null intermediate receivers and noninterference remain conditional assumptions.
No stack capture, reflection, application code or SQL is executed by the extractor.

Intentional versioned trace-chain limit 128 (was 64), with 32 stack-name calls/chain,
16 distinct local stores/chain and frame indices 0..31. Existing stack 16, array 16,
string-computation 32, chains 8, heap 256, body 256 and aggregate work/call-depth bounds
remain. Local stores preserve modeled values and fresh-array allocation identity;
array writes still require an array allocated inside that trace expression. Field
loads/stores have no bypass. Unsupported default and read roots remain strict.

Trace refusals now optionally expose externalTraceFailure: exact body/binary/
invocation/token/offset, opcode, reason, optional call signature hash/shape and at
most16 abstract stack kinds. This diagnostic contains no source snippets, message
values or runtime stack names. Existing generator/input provenance binds it.

Independent public C#/VB fixtures and test definitions cover short local-array
and eight-capture expressions, exact identity mutations, dynamic frames, general
reflection, strict default and stack-reference local escape. CLI/test-project and
both fixture projects compiled with --no-restore -c Debug -warnaserror, zero
warnings/errors. Tests and native scans were NOT run, deferred by Joe. Work-machine
prompt permits one isolated build and one scan only, then stop.

Why earlier checks missed this: the selected fixture coverage did not assert a
compiler-generated local-array spill together with stack-name reflection. Compile
success checks types, not interpreter behavior, and authored tests were unexecuted.
Generic trace failures omitted instruction/offset, while dirty-tool progress was
not equivalent to clean-public-head behavior. The new trace diagnostic addresses
that evidence omission; it does not justify accepting the unpublished field bypass.

Known remaining limits are not a count of private blockers: general reflection,
other stack constructors/dynamic frames, reference escape, arbitrary argument
helpers, branch/handler/loop-crossing trace expressions, and non-custom field
receivers remain unsupported. A later field-receiver failure was reported only
from a dirty tool; its exact operation is still unproven on the clean path. Native
entry lineage, owner-schema comparison and overall SQL completion remain unproven.

## Exact opcode attribution and native gap wiring — 2026-10-29

Extractor 0.2.20 / policy v21 fixes a diagnostic integration omission: interpreter
FieldAccessFailure and ExternalTraceFailure were not serialized onto AnalysisGap
facts. They now appear as fieldAccessFailure and externalTraceFailure properties.
Earlier absence of these properties could not establish successful or unreached
phases. This corrects prior handoff assumptions. No admission changes are made.

Generic unsupported-opcode refusals now emit opcodeFailure with selected-root
metadata, actual failing body/binary/token/offset, retained instruction count and
a snapshot of the actual bounded interpreter invocation path. Each frame records
its entry/invocation and caller site; the path uses the existing depth 24 bound
and is captured before unwind. Constructor entry is distinguished by selected-root
metadata even when the initial frame is the root constructor. Body-shape refusals
also retain bodyMetadataFailure. Invocation evidence is not runtime reachability.

Code inspection confirms selected-root slicing follows forward reference edges
plus the root constructor, and interpretation invokes exact matched call targets.
A body being present in the corpus/slice does not itself execute that body. An
unreachable sibling's opcode gap therefore requires checking exact fact rule/root,
module-local token AND binary, actual call resolution and receipt provenance;
no corpus-wide interpretation cause is established by the private report.

Public C#/VB delegate-refusal fixtures and regression definitions cover nested
opcode-path attribution, emitted native trace/opcode diagnostics and separation
of an unrelated delegate root from a selected producer. Tests/scans remain deferred
by Joe. The prompt permits at most one isolated build and one scan, then stop.
No LINQ support, delegate bypass, field bypass, bound increase or completion claim.
