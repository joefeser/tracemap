# Compiled SQL lifecycle capability map — public interpreter state (IL 0.2.27)

Public, sanitized capability map of `IlSqlLifecycleExtractor` and its opt-in
boundaries against the bounded compiled wrapper protocol, derived from the public
synthetic fixtures and interpreter source only. Inventory is not proof of runtime
reachability on any private body. Version `il-body-evidence/0.2.32`, evidence
policy `explicit-il-body-evidence.v33`, state control `explicit-sql-state-control/1`.

## Scope

Root shapes: entry producers returning `DataSet`, void loaders, guarded
(single-Boolean-argument) roots and column-read roots. Callee closure: exact
signed mscorlib/System.Data net48 (+ retargeted net20 profile) members below plus
admitted bodies in the same compiled inputs. Bounds: 4096 indexed bodies, 256
instructions/body, 24 depth, 8192 steps + shared work budget, 256 heap objects,
64 stack/local slots, 8 executions, 16 parameters/collection items.

## Supported protocol surface (per public fixtures, C# and VB)

| Stage | Supported | Boundary/refusal |
| --- | --- | --- |
| Configuration | `SqlConnection(string)` from string/input/opaque scalar | connection-shape refusal otherwise |
| Construction | zero-arg tracked constructors; metadata-proved direct-base constructor field views; opt-in opaque root scalar initialization | unrelated owners never interchangeable |
| Parameter objects | inspected custom fields/properties OR constructor-proven `SqlParameter` allocations added directly to the lifecycle ArrayList (frozen on Add, typed-foreach `castclass` passthrough, attached with exact identity); **value-type parameters constructed in place** (compilers lower struct construction to `ldloca` + `CALL`, boxing for `Add(Object)`, `unbox.any` on foreach Current, `initobj` prologues — all identity-preserving); constant strings join unfrozen with their exact text | mutation/escape after freeze refuses; null, unknown and other item kinds refuse |
| Collection | exact net48 ArrayList: new/Add/GetEnumerator, one complete enumeration ≤16 | second enumeration, mutation, unsupported collections refuse |
| Attachment | `SqlCommand(name, connection)`, CommandType, `SqlParameter(name, type, size)`, `set_Value` (scalar origins), `Parameters.Add` | reuse after attachment refuses |
| Execution | `SqlDataAdapter.Fill` per command/dataset pair, execution identity per Fill | second Fill/merge into used shapes refuses |
| Results | returned `DataSet` allocation, stored dataset fields, table states, per-column origins | origin-less returned dataset refuses |
| InfoMessage | exact delegate ctor/subscribe/unsubscribe, callback inspection (names/naming lists), post-Fill naming receipts, framework 2/4 profiles | callback execution, sender access, list escape refuse |
| External trace | exact System.Web `TraceContext.Write` boundary incl. computed strings, stack-name chain, trace locals; the `StackTrace`/`GetFrame`/`GetMethod` name chain also resolves in non-callback lifecycle bodies under the flag (every step unbound, frame index 0-31) | arbitrary argument computation still bounded; without the flag the chain refuses |
| Runtime State | exact signed `SqlConnection/DbConnection.get_State` reads (InfoMessage opt-in): unbound observation + receipt | wrong key/version/shape refuses |
| Command config | `set_CommandTimeout`, `set_Transaction(null-only)`, `set_Direction` (pre-attachment), 4-arg source-column `SqlParameter` ctor, `TableMappings.Add` | tracked/mutated operands refuse |
| Typed arrays | `ArrayList.Clear` (pre-freeze), `ToArray(Type)` snapshot, exact-count indexed loops (`ldlen`/`ldelem.ref`/`conv.i4`/`add` + backward `blt`/`clt`+`brtrue` bounded by array length, C# and VB lowerings), `newarr` String arrays (constant length ≤16, exact slot stores) | non-array comparisons, mutated-after-freeze refuse |
| VB scalar helpers | `Interaction.IIf` (concrete Boolean only), `Conversions.ToInteger` (Int32 only) | state-test conditions, non-Int32 operands refuse |
| Strings & reflection | `DBNull.Value` (the one admitted static field, a constant), `starg` parameter reassignment, `GetObjectValue` identity for inputs, `GetType` (untracked result) followed by `Type.ToString` or the `Object.ToString` virtual slot (both unbound, no type-name or representation claims — the .NET 1 type-name pattern); `String.Concat` 2-4 args (compiler folds chained concatenation; exact for all-constant operands, unbound otherwise), `Object.GetType`, `MethodBase.GetCurrentMethod`, `MemberInfo.get_Name`, `Exception.get_Message`/`get_InnerException` (unbound strings, no runtime claim) | other uses refuse |
| Event log | `EventLog.WriteEntry(String,String)` static terminal under the external-trace boundary flag, trace-scalar args only | non-trace args, callback context refuse |
| Configuration | the `ConfigurationManager` chain in the root constructor becomes opaque field origins under `--il-opaque-root-initializers` (three-call chain receipt); no catalog entries needed | without the opt-in: unknown-call refusal |
| State control | comparison of one unbound State against a compiled constant forks bounded path alternatives (≤32 paths, ≤8 sites, per-alternative identities; forks extend the CURRENT assignment so both-sides-completing decisions — state guards, input nullness — compose as a proper decision tree), including the Roslyn zero-constant lowering `State != <0-member>` → `ldc.i4.0 + cgt.un`; **an input argument's nullness is likewise a bounded hypothetical** (null-assumed / non-null-assumed alternatives, keyed by input identity so every guard on the same input shares one fork — the .NET 1 null-guard + `DBNull.Value` substitution pattern); direct-throw construction + uncaught throw terminates its alternative; early leave abandons an active loop without entry evidence | state-vs-state, non-constant comparisons, caught throws, backward state branches, over-budget forks refuse |
| Control flow | forward branches; single enumerator loop with loop/collection tests; try/finally via leave/endfinally; ceq on concrete ints/null/fresh aliases; negation/identity lowering of one state comparison | unproven loops, loop-carried fields, comparison-branch family (beq/bne.un/bge/…) and relational ops refuse |
| ByRef aliasing | `ldloca`/`ldloca.s` local addresses pass into byref parameters of tracked inlined callees (ECMA-335 III.3.42 shape; the compiler lowers ByRef call sites in both languages); reads/writes alias the caller's local slot through the call (`ldind.ref`/`stind.ref`, implicit dereference as receiver/element target); the mutated collection/array is used with its exact identity after return | addresses reaching a framework argument, a field store, an array element, a return value, a constructor, or a stale frame refuse; framework ByRef APIs stay unmodeled |

## Opcodes admitted in entry bodies

nop, ldarg/ldloc/stloc (incl. .s/.N), ldstr, ldnull, ldc.i4*, ceq (concrete +
state-test), dup, pop, box (input/Boolean/Int32), ldtoken, newarr (Object; String
only in the trace root), stelem.ref, castclass/isinst (tracked shapes), br/br.s
(forward), brtrue/brfalse (incl. state-test), leave/leave.s + endfinally,
ldftn (InfoMessage delegate), ldfld/stfld (owner-proved), call/callvirt/newobj
(catalogued framework APIs, admitted bodies, direct-throw construction), ret,
throw/rethrow (termination/refusal semantics above). Everything else refuses with
opcode + offset + invocation path diagnostics.

## Identity rules

Allocation IDs are per-run; deterministic re-execution gives the shared prefix
before the first state decision identical IDs. Each alternative numbers its own
allocations from the same scheme: equal IDs across alternatives denote the same
prefix slot, never a merged object. Executions, attachments, parameters and entry
evidence are per-alternative and never merged into unconditional claims. Every
`StateDecision` records site, connection, constant, comparison, occurrence and the
assumed outcome; constants are compiled inputs, never observed runtime values.

## Known gaps (explicit, not silent)

- No general comparison-branch opcodes (`beq`/`bne.un`/`bge`/…) or relational
  `clt`/`cgt`: the admitted shapes are the array-bounded loop tests, the concrete
  int/array-length comparisons and the zero-constant `cgt.un` state test; other
  comparison shapes refuse.
- No LINQ interpreter, no general SQL parser, no `DataTable.Select`, no live SQL,
  no deployment or acceptance claim.
- Catch continuations are never executed; uncaught throws do not run crossed
  finally handlers (recorded in the termination limitation).
- A state read inside a loop body is one symbolic-pass decision per occurrence;
  multi-iteration state variation exceeds the path budget and refuses.
- Runtime `State` value, transitions, and stability across reads remain unproven
  by design; receipts carry `no-enum-value-or-transition-observed`.

## Work-machine reconciliation

This map is derived from public fixtures only. The bounded read-only pass
reconciling it against the two selected private inner roots ran as the
v12-001 validation (dispatched as a working prompt, not retained in the
tree); its record follows.

## Work-machine reconciliation (v12-001, 2026-10-30)

Reconciled against the one bounded read-only inventory of the two selected private
inner roots (19 retained bodies, ~1,160 instructions, dual-reader agreed; measured
by an 0.2.25 tool, so its first-gap ordering predates the 0.2.26/0.2.27 fixes).
Private identifiers, binary hashes and tokens are intentionally absent here.

**Already covered at 0.2.27** — the inventory's headline blocker and most of its
phase list: the three `get_State` guard sites (both framework profiles, both
constant kinds, exactly the ceq + brfalse/brtrue lowering the state-alternatives
engine forks on); the delegate-construction pair (`ldftn` + handler ctor);
`DataSet`/`SqlCommand(name,connection)`/`set_CommandType`/`get_Parameters`/
`Parameters.Add`/`SqlDataAdapter(command)`/`Fill`; `SqlParameter.set_Value`;
`new ArrayList()`; `Type.GetTypeFromHandle`. The inventory's context-sensitive
refusals (SQL execution or nested Open in the callback, dynamic SQL from the
message, stack/reflection or event-log access in the callback, the Int32 XML
attribute overload) are all already enforced refusals, not gaps.

**Next bounded batch — genuinely missing adjacent signatures (~15):**
`set_CommandTimeout`, `set_Transaction`, `get_TableMappings` +
`DataTableMappingCollection.Add(string,string)`; the 4-argument `SqlParameter`
ctor, `set_Direction`, `Microsoft.VisualBasic.Information.GetObjectValue`
(pass-through), `Object.GetType` (untracked result); `ArrayList.Clear` (bounded:
before freeze only) and `ArrayList.ToArray(Type)` plus whatever array-enumeration
shape sits behind it — the inventory is first-gap ordered, so each unlock can
reveal further signatures that never executed; `Interaction.IIf` (concrete-bool
selection only; a state-test condition still refuses) and `Conversion.ToInteger`
(int argument only); `String.Concat` (both overloads) as computed-string values;
and the reflection-identity reads (`MethodBase.GetCurrentMethod`,
`MemberInfo.get_Name`, the `StackTrace`/`GetFrame`/`GetMethod` chain) — the latter
as an extension of the existing trace-stack-names boundary semantics to non-callback
lifecycle bodies under the external-trace flag, never inside callbacks.

**Verify-against-boundary before cataloging (per the inventory's own caveat):**
the configuration chain (`ConfigurationManager` → connection string) likely becomes
opaque field origins through the opaque-root-initializer boundary rather than new
catalog entries; the crypto helper (~15 signatures) likely collapses into one
inspected compiled scalar transform under `--il-compiled-scalar-boundary`;
`HttpContext.Current`/`TraceContext.Write`/`EventLog.WriteEntry` belong to the
external-trace boundary (root bodies only; `EventLog.WriteEntry` would be new if
verified). No entry from these groups is added without that verification.

The public synthetic blueprint in the inventory matches the whole-protocol
fixtures committed here; the next batch extends them with the leftover signatures
above plus their refusal controls.

## Synthetic closure enumeration (2026-10-30, IL 0.2.29)

A public synthetic clone of the v12-001 19-body closure (two inner roots plus
connection/adapter-build/parameter/exception/crypto-shaped/table-mapping/debug
helper bodies, C# and VB, `ClosureShape` fixtures) was executed through the
interpreter with all five boundary flags. Every refusal was enumerated and
classified. **No layer-A (ECMA-335 decoding) stop exists in the closure**: every
instruction decodes identically in both readers and every refusal is admission
or bounded-semantics, not decoding.

Implemented by this batch (found only by synthesis):

- `MethodBase.GetCurrentMethod` catalog identity was malformed in 0.2.28 (the
  owner type encoding had never been executed by a fixture); fixed and now
  fixture-proven in both languages.
- `String.Concat` 3/4-arg: both compilers fold chained string concatenation
  into the 3-argument overload even when parenthesized pairwise; added with the
  same exact-constants/unbound semantics.
- The `StackTrace`/`GetFrame`/`GetMethod` name chain now resolves in non-callback
  lifecycle bodies under the external-trace flag (v12-001's predicted extension),
  refusing without the flag.
- Roslyn lowers `State != <zero-constant member>` (e.g. `ConnectionState.Closed`)
  to `ldc.i4.0 + cgt.un`; that lowering is now a state test that forks bounded
  alternatives instead of refusing.
- `UnboundStringCount` only propagated on failure results, so completing results
  could never carry the `computed-string-values-unbound` claim token; fixed.

Enumerated refusals (kept, with fixture pins):

| Stop | Classification |
| --- | --- |
| `DataTableMappingCollection.get_Item`/mapping content reads | layer C, out of protocol scope (only the mappings View and `Add` are admitted) |
| custom exception base call `Exception..ctor(string, Exception)` | layer C if a private body constructs a custom exception type; the covered shape is direct-throw construction of framework exception types |
| framework exception construction as a method argument (not throw-adjacent) | by-design: untracked construction is admitted only immediately before `throw` |
| input-driven parameter sizes (`Number` requires compiled constants) | by-design: required scalar values refuse |
| non-primitive root arguments (any parameter outside the primitive whitelist untypes every root input) | by-design |
| `Information.IsNothing` on a proven-null operand (`lifecycle-guard-unresolved`) | by-design: the proven-null fold is admitted for the comparison forms (`ceq`, `cgt.un`/`clt.un`); the `IsNothing` call form stays a declared boundary until a scan evidences it |
| `lifecycle-info-unmatched-unsubscribe` (admitted 2026-11-02, batch 37) | an InfoMessage unsubscribe whose handler is not in the connection's tracked subscription set records `unsubscribe-external` (the detach executes concretely; the external subscription stays unproven). The handler must still pass the tracked-shape checks; an untracked handler form keeps refusing |
| crypto-shaped transform without the compiled-scalar boundary | by-design: the boundary is the opt-in |
| stack-name chain without the external-trace boundary | by-design: the boundary is the opt-in |

## v14-001 outcome B — ByRef batch (2026-10-30, IL 0.2.30)

The final guarded validation stopped at `lifecycle-opcode:ldloca.s` (root offset
198, between the ArrayList construction and the first parameter constructor):
the private roots pass an `ArrayList&` and a `String[]&` by reference into the
execute helper. The opcode decoded correctly in both readers — the gap was
calling-convention semantics, not ECMA-335 decoding, so the sufficiency answer
(no layer-A work) stands with one amendment: argument passing conventions were
not represented in any synthetic fixture before v14, so the closure had never
enumerated them.

Implemented here with the same bounded design: local addresses alias one live
caller slot, pass only into byref parameters of tracked inlined callees, never
reach the heap/framework/returns, and `newarr` String arrays (the `String[]&`
output shape) are now admitted in lifecycle bodies with constant length ≤16 and
exact slot stores. Pinned refusals: framework ByRef APIs (`out`/`ByRef` into
`int.TryParse`), addresses escaping to fields/elements/returns, indirect stores
outside local addresses. No private input was used; the fixtures are the public
`ClosureByRef` shapes.

## v15-001 outcome B2 — raw-parameter collection batch (2026-10-31, IL 0.2.31)

The v15 final validation advanced through the entire 19-body closure and stopped
at `lifecycle-object-origin:custom` (unconditional, both roots, no inline
failure object). The reporting interpretation ("lifecycle host-object
provenance") was incorrect: the actual throw site is the lifecycle collection
item admission — `ArrayList.Add` requires a tracked custom parameter object.
The gap was reproduced synthetically first (`ClosureRawParameterProduce`:
constructor-proven `SqlParameter` allocations added directly to the lifecycle
ArrayList refuse at exactly `lifecycle-object-origin:custom`), then admitted
boundedly: parameter-kind items join custom objects in the collection (frozen
on Add), the typed-foreach `castclass` on SqlParameter/DbParameter passes
through, and enumeration attaches them with exact allocation identity. String
items remain a pinned by-design refusal (`ClosureNameListProduce`).

Residual honesty note: the sanitized v15 report does not reveal WHICH non-
custom item shape the private code adds. Raw SqlParameter additions are the
strongest candidate (real ASP.NET pattern, matches every observable: bare
refusal, unconditional, before any state read). If the private item is a
string or another kind, a v16 pass would stop at the same gap again — the
disambiguating sanitized ask is: the offset of the refusing `ArrayList.Add`
in the root body plus the identity of the call that constructed its argument.

## v15 follow-up — string-item batch (2026-10-31, IL 0.2.32)

The sanitized disambiguation (first ArrayList element = a cross-assembly
memberref to the custom parameter structure four-argument constructor)
proved two things. First, cross-assembly memberref resolution already works:
body identities are memberref-form and the end-to-end fixtures have exercised
cross-assembly custom types since the beginning; the private ctor is retained
dual-reader-agreed in the slice. Second, since the first element is a plain
custom object, the v15 `lifecycle-object-origin:custom` stop must have fired on
a LATER item kind. This batch admits the remaining sound shape: constant
strings in the lifecycle ArrayList (unfrozen, exact text, ≤16 cap), with the
two compiler lowerings they bring (C# typed-foreach `castclass String`; VB
`Conversions.ToString(Object)` — where an identity conversion of an
already-exact string now stays exact instead of being intercepted as an
unbound trace-string computation). Null and unknown items stay refused.

Fixture note: the cross-assembly root now uses the EXISTING wrapper
`InputParameter(String, SqlDbType, Int32, Object)` — the exact reported
signature — because the native scan shares ONE 1 MiB object-body budget across
all compiled inputs, and growing the wrapper assembly dropped end-to-end tail
bodies (Projection) which broke unrelated pinned tests. The
`Private="false"` project reference keeps the topology without copying
binaries into the scan's input set.

## v16-001 follow-up — self-describing object-origin diagnostics (2026-10-31, IL 0.2.33)

v16 stopped at the same `lifecycle-object-origin:custom` gap with the same
19-body slice, but its B1 classification is internally inconsistent: the
reported items are custom parameter-structure objects, custom items are
admitted, and the public cross-assembly fixture with the identical four-field
signature completes green in both languages and the legacy profile. Three
consecutive report interpretations of this one message have now been
contradicted by the code, so the interpretation layer is being removed: every
object-origin refusal now attaches `objectOriginFailure` to its gap fact with
the kind actually found, the kinds expected, and the in-flight call identity
(+ body and offset). The refusal message text is byte-stable — exact-match
catch lists and pinned expectations are untouched. Null items stay a pinned
refusal (`ClosureNullItemProduce`) and carry the diagnostic, proving the
wiring. Any future stop of this class names itself in the report.

## v17-001 root cause — value-type parameter batch (2026-10-31, IL 0.2.34)

The self-describing diagnostic ended the three-report mystery in one scan:
`found: "unknown"`, `expected: "custom"`, refusing call = the
the parameter-structure four-argument constructor dispatch itself.
Root cause: the private parameter type is a value type (.NET 1 heritage), and
compilers lower struct construction to `ldloca + CALL` on the destination
local (not `newobj`); the base-ctor delegation helper demanded a custom-kind
receiver before checking its own eligibility, so the uninitialized local
surfaced as a bare `lifecycle-object-origin:custom` — which every report (and
two agents) misread as the ArrayList item admission. The batch admits the
whole struct lifecycle, identity-preserving: construction writes a tracked
custom into the addressed slot; `box`/`unbox.any` of tracked allocations pass
through; `initobj` zeroes a local slot or a ctor's own prologue allocation;
the constructor-owner-view checks eligibility before any kind demand.

## v18-001 — input-null fork batch (2026-10-31, IL 0.2.35)

The value-type batch cleared the struct admission; v18 advanced into the
the parameter-structure constructor and stopped at `ceq` offset 25:
`lifecycle-equality-unresolved:input:null` — a .NET 1 null-guard on an
input-supplied argument. The v12-era rule ("no inferred nullness for inputs")
became a refusal exactly where the private closure lives. It is now the third
member of the hypothetical family: an input's nullness is one runtime fact, so
the decision is keyed by input identity (every guard on the same input shares
one fork instead of multiplying paths), the null-assumed and non-null-assumed
alternatives carry per-alternative identities, and the classic substitution
branch works end to end (`ldsfld DBNull.Value` constant, `starg` parameter
reassignment, `GetObjectValue` identity passthrough for input objects). The
EventNamingUnknownEquality pin moved from refusal to the fork semantics.

## v19-001 — fork-tree driver batch (2026-10-31, IL 0.2.36)

The input-null forks exposed a latent structural limitation in the alternatives
driver: forks were gated by a GLOBAL visited set, which assumed every fork's
throw-side terminates (true for state guards). Input-null decisions have BOTH
sides completing, so sibling subtrees legitimately re-encounter a key forked
elsewhere — the global gate misread that as a loop and refused with
lifecycle-state-path-limit regardless of the bound. The driver now forks by
extending the CURRENT assignment (per-path decision dictionaries — a proper
decision tree), with a defensive invalid-refork refusal if a decided key ever
re-forks on its own path. Budgets rose to 32 paths / 8 sites (capacity, not
admission: every path remains individually proven, no selection or merging);
the ManyGuards fixture grew to six two-sided guards so the beyond-budget
refusal stays pinned.

## v20-001 — GetType scoped-return fix (2026-10-31, IL 0.2.37)

The v20 gapKind carried the full refusing identity: `System.Object.GetType()`
with a SCOPED mscorlib `System.Type` return. The catalog entry had been written
with an unscoped return encoding and — like GetCurrentMethod in batch 4 — was
never executed by a fixture, so the mismatch only surfaced when the private
ctor loop called GetType on mscorlib 2.0.0.0. The entry now carries the scoped
return (both framework profiles resolve through the automatic legacy rewrite),
and GetType executes in the public collection-loop fixture with an untracked
result, closing the never-executed-entry class from batch 3.

## v21-001 — type-name string batch (2026-10-31, IL 0.2.38)

One instruction deeper than GetType: `System.Type.ToString()` on mscorlib
2.0.0.0 — the .NET 1 type-name pattern. Both binding forms are admitted with
identical no-claims semantics: `Type.ToString` (how the private call and VB
bind) and the `Object.ToString` virtual slot (how C# binds `.ToString()` on a
typed result) each return an unbound string whose origin names the pattern;
no type-name or representation value is ever invented. The public fixture now
executes the full GetType().ToString() chain in the collection loop.

## v22-001 — VB string-comparison batch (2026-10-31, IL 0.2.39)

The predicted consumer arrived: VB lowers `typeName = "..."` to
`Operators.CompareString(String, String, Boolean)`. With an unbound type-name
operand the equality outcome is a runtime fact — the same hypothetical class
as input nullness — so the call returns a hypothetical comparison and the
`ceq`-against-zero lowering forks bounded alternatives (site+occurrence keyed,
per runtime fact; the decision records `string-compare`/`0`). Both framework
profiles resolve (VB 8.0.0.0 through the automatic rewrite). The public VB
fixture executes the full GetType().ToString()=constant chain and compounding
forks (input-null x string-compare x per-invocation) complete within the 32/8
budget with per-alternative entry evidence; C# compiles the same source to
String.op_Equality and is unaffected. Honest forward note: the private ctor is
invoked four times — if each invocation forks its own string comparison the
decision count multiplies and the 32-path budget may refuse; that refusal
would be evidence for the budget-vs-keying question, not a silent guess.

## v23-001 — VB conversions batch (2026-11-01, IL 0.2.40)

Two facts from this scan: the 32-path budget HELD under the CompareString
forks (wall clock 45 s — the deepest execution yet), and the stop was an
overload, not a version — the private code calls
`Conversions.ToInteger(String)` (the `CInt(string)` lowering, VB 10.0 from the
App_Code compile-time reference) where the catalog had only the Object
overload. The String overload is admitted with honest semantics: an int
passthrough and a typed Int32 input stay as before; a runtime string parses
culture-dependently, so the value stays unproven (Unknown) — execution
continues and every downstream proof demand (parameter sizes, arithmetic)
keeps its named refusal. The mixed VB runtime versions across the private
binaries (8.0 in the framework assembly references, 10.0 in the app compile-time reference — unified
only at runtime) continue to resolve through the dual-profile catalog.

## Static-type collapse batch (2026-11-01, IL 0.2.41)

Joe supplied the four-overload ctor shape from the source: null-guard to
DBNull, the type-name dispatch on "System.String" (correct case — a live
branch), a nested inline empty-string-to-DBNull check, then value and
direction assignment; two overloads carry ParameterDirection and two derive
size (one size-less overload uses the two-argument SqlParameter constructor,
now catalogued). The arithmetic of naive forking — null × type × emptiness
across four invocations — exceeds any sane path budget, so this batch makes
the type system collapse it soundly:
- GetType on a statically typed value derives the exact runtime type name
  (string/int constants, DBNull, String/Int32-declared inputs); polymorphic
  values stay unknown.
- Type.ToString / the Object.ToString slot render a derived type exactly;
  String.ToString is identity; unbound results carry the RECEIVER's
  provenance so distinct values keep distinct facts.
- CompareString with identical exact operands is equal under any culture;
  two all-ASCII exact constants compare ordinally.
- String-compare forks are keyed by the LEFT operand's provenance (one
  runtime value's equality is one fact, shared across uses and invocations).
The confirmed private ctor shape now runs end-to-end in the public fixture:
4 alternatives (null × type-name) where the naive model would need thousands.

### Declared hazard: Option Compare Text (flagged 2026-11-01)

The concrete string-comparison rules assume BINARY compare (the confirmed
sample sets no `Option Compare`): identical texts are equal under any mode,
and the all-ASCII ordinal shortcut applies ONLY when the CompareString
text-compare argument is false (binary). Under `Option Compare Text` the
comparison is culture- and case-aware, so every CompareString stays a bounded
hypothetical fork — the ordinal shortcut is deliberately disabled in text
mode. Any future sample that sets `Option Compare Text` at file or project
level must re-validate these rules; parameter direction passthrough from
callers (the direction overload) is likewise recorded as a fixture shape.

## Inverted-comparison batch (2026-11-01, IL 0.2.42)

Pre-admitted from the ECMA-335 hint (there is no ceq.un — compilers lower
"not equal" to bne.un or a ceq inversion): the beq/bne.un equality branches
resolve for the operand kinds already proven (concrete ints; string-compare
booleans and state tests fork with the branch polarity), and VB's lowering of
string `<>` — CompareString followed by cgt.un against zero, the nonzero
test — produces the negated equality hypothetical. Pinned in both fixtures;
the nested `<>` fork composes with the `=` fork (6 alternatives on the
null-guard root, all completing).

## Unbound-integer batch (2026-11-01, IL 0.2.43)

v24/v25 stopped at a bare `lifecycle-integer-unresolved` with no site
identity. Reproduced locally first (no dispatch spent): CInt over a runtime
string — whose parse stays unproven — fed as the SqlParameter SIZE hits the
Number() demand. The fix follows the unbound-string design: CInt runtime
parses produce an unbound integer; the SqlParameter constructors accept it as
UNBOUND SIZE EVIDENCE (the entry records a null size — never a number — with
the parameter claims carrying the unbound token), and every other integer
demand keeps its refusal. Number() refusals now attach a `numberFailure`
diagnostic (found kind + in-flight call identity; message byte-stable) so any
future integer stop names itself, the way objectOriginFailure ended the
v15–v17 mystery. The fixture also mirrors the confirmed size-less overload
(the two-argument SqlParameter, no ctor chaining).

## Full-root mirror + COBOL period slicing (2026-11-01, IL 0.2.44)

Joe shared photos of the actual root methods (read locally with WEE; shapes
only, no private identifiers in artifacts) plus the domain context: the
source of truth is 1970s COBOL/FORTRAN — 2-position months as
string-sometimes-int, 6-digit YYYYMM bid periods. Admitted accordingly:
- the full-root mirror fixture (trace preamble with the four-frame stack
  chain, boxed CInt(Integer) into the (name,type,Object) overload, a
  VarChar(-1) max-size constant, the two-argument ExecProc, close, return)
  completes with per-alternative evidence
- Int32-DECLARED inputs and unbound integers box without demanding parsed
  constants; unbound integers carry nullness forks and GetObjectValue identity
- String.Substring(Int32,Int32) and VB Strings.Mid(String,Int32,Int32)
  produce unbound strings carrying the receiver's provenance — the period
  slicing (Mid("202604",5,2) style) feeding CInt into unbound integer
  parameter values completes end to end

## Boxed-value-type collapse (2026-11-01, IL 0.2.45)

v26-001 re-hit the 32/8 path budget under combined period-slicing, nullness
and type-dispatch forks. The budget was NOT raised: the multiplication was
unnecessary. A CInt result is a boxed Integer — statically never null and
always System.Int32 — so both guards on such values resolve CONCRETELY
(null-guard false, type-name dispatch "System.Int32" ≠ "System.String"),
collapsing two fork families per unbound integer to zero. CInt results also
carry their source provenance now (distinct slices keep distinct facts for
diagnostics). The only forks a root pays are its genuine runtime unknowns:
string inputs' nullness and content-emptiness, plus the connection state
guards — 16 paths for the confirmed root shapes, well inside the budget, with
no wall-clock risk (a budget raise would have approached the 60-second scan
guard).

## Unbound-int comparison batch (2026-11-01, IL 0.2.46)

v27-001 stopped at a bare lifecycle-comparison-unresolved (no site identity):
an unbound integer compared against a compiled constant — the range check
pattern (CInt(month) > 0). The relation of one unbound integer to a compiled
zero/one constant is a runtime fact: it now forks a bounded hypothetical
keyed by the value's provenance (one value = one fact, shared across uses),
with the cgt.un/clt polarity lowering handled and a comparison diagnostic
(kinds + in-flight call) attached for any future stop of this class.

## Reference non-null test batch (2026-11-01, IL 0.2.47)

v28-001's numberFailure diagnostic named the site in one scan: the
the root execute-helper connection guard — `IsNot Nothing`, which Roslyn lowers to
`cgt.un(x, ldnull)`, a reference non-null test the comparison handler never
covered. A provably non-null allocation (tracked object, exact string, boxed
value type) now resolves concretely to true; a maybe-null value (typed
reference input, unbound string) forks its nullness hypothetical (the negated
input-null test). The public exec helper carries the guard as a fixture.

## Fork-census diagnostic (2026-11-01, IL 0.2.48)

v29-001 re-hit the path limit at a deeper site with no diagnostic on the
refusal. The path limit now carries its own evidence: run counters (runs,
alternatives, queued, visited sites) and the per-fact fork census — which
decision families consumed the budget and how many times — attached as the
pathLimitFailure gap property (message byte-stable, the batch-8 pattern).
The next path-limit scan states whether the budget is spent on genuine
runtime unknowns (raise it, with wall-clock eyes) or on a collapsible
multiplication.

## Evidence-driven budget raise (2026-11-01, IL 0.2.49)

The v30 census decided the deferred budget question with data: six visited
sites, all distinct facts (four input-null values keyed by provenance, one
CInt-derived nullness, one state guard) — genuine runtime unknowns, no
multiplication. The product 2^5 filled the 32-path queue with zero
completions. Budget raised to 128 paths / 12 sites (runs cap scales to 256);
the exploration cost per run is bounded interpretation work, and the mirror
fixture extended to the census shape (four nullable inputs, six parameters)
completes with 65 alternatives at the PRODUCTION work limit (2M units — the
400K test harness budget was itself the next wall). ManyGuards raised to
eight two-sided guards so the beyond-budget refusal stays pinned (256 > 128).

## Exception-ctor admission + input-bool forks (2026-11-01, IL 0.2.50)

Joe supplied the confirmed shape: the custom exception ctor takes
GetCurrentMethod().Name, two command texts and an inner that may be the
caught exception or a framework exception constructed as an argument
("stupid reason"). The v31 B1 boundary became an admission: the
base-constructor call on a tracked allocation is constructor completion
(framework internals unmodeled); a framework exception newobj anywhere
produces the same untracked external-exception value the direct-throw
adjacency produces — every legal continuation stores into a to-be-thrown
object or throws. Also: a Boolean-DECLARED input branched in a callee
(the fail-style guard) forks a provenance-keyed truth hypothetical, moving
the guarded-root semantics to any Boolean input. Fixture: ClosureGuardException
with the exact four-argument shape; the throw side terminates, the
completing side carries the entry. Queued from the ExecProc_Scalar photo
(WEE-local): the loop-carried string-accumulator field, transaction-from-
field, and ExecuteScalar terminal for the next roots.

## Exception.ToString admission (2026-11-01, IL 0.2.51)

v32-001 advanced past the exception-ctor admission and stopped at
Exception.ToString() on the caught/stored exception — the diagnostics
pattern continuing the family. Admitted with the get_Message semantics:
an unbound string, no representation claim, both framework profiles. The
guard fixture wires ToString of a constructed inner through the exception
argument.

## Tracked-collection non-null (2026-11-01, IL 0.2.52)

v33-001: the same ExecProc guard site, second guard — the ByRef ARRAY
parameter's IsNot Nothing test. Tracked array snapshots, ArrayLists and
lists join the provably-non-null kinds in both the reference non-null test
and the equality Fresh predicate (a tracked collection allocation is never
null). Queued battery (Opus-diffed): ExecuteScalar/NonQuery terminals,
IsNothing/IsDBNull, sub.ovf, get_Count/get_Item indexed loops,
ConcatenateObject/Conversions.ToString, the Merge framework-pending fix.

## Opus-review batch (2026-11-01, IL 0.2.53)

An external model review of the interpreter (second pass, correct head,
Roslyn-verified lowering probes) found a correctness bug and four
inconsistencies, all fixed or declared here:
- **String-comparison polarity**: the raw CompareString result (Int32
  convention, 0 = equal) and the op_Equality result (Boolean convention,
  1 = equal) shared one marker while brtrue/beq read it as Boolean — in
  Release lowerings (CompareString; brtrue ELSE) the equal-assumed
  alternative walked the not-equal side: full path coverage, mislabeled
  claims. The markers are now distinct, every consumer normalizes (Int32
  negates at branch consumption; ceq/cgt.un/beq match the compared
  constant), the compared CONSTANT is baked into the fork key ("x = A" and
  "x = B" are distinct runtime facts), and a dedicated polarity fixture —
  plus a Cecil-rewrite test that converts the compiled Debug ceq sequence
  into the Release brtrue-direct shape — pins the equivalence.
- **Object-path crash**: the two-argument SqlParameter ctor read args[2]
  unconditionally — an IndexOutOfRangeException, not a named gap; guarded.
- **iif** now accepts DBNull and unbound integers (IIf(c, DBNull.Value, x)).
- **DataSet.Merge** is a declared lifecycle refusal (merge-unmodeled)
  instead of an accidental framework-pending fall-through.
- Queued from the same review: indexed For loops (sub.ovf + length-k bounds
  + .s forms), get_Count/get_Item, IsNothing/IsDBNull, the
  ConcatenateObject+Conversions.ToString pair, typed newarr family, ByRef
  pass-through roots, GetObjectValue on tracked objects, UBound/CopyArray,
  EventLog legacy scope, and/or opcodes.

## Typed element loads (2026-11-01, IL 0.2.54)

v34-001 advanced past the array guard to a bare `ldelem` (the operand carries
the element type): typed loads from TRACKED arrays read their exact slot
(in-range enforced); elements of untracked arrays — the ByRef String()&
output parameter the callee fills — stay unknown with no claim. The
output-names fixture pins the shape end to end (the consumed read completes
the run). Fixture lesson: consumption by assignment keeps the element out of
trace-scalar and nullness demands; guard-shaped consumption invents new
boundaries unrelated to the pin.

## Typed-array newarr (2026-11-01, IL 0.2.55)

The bare `ldelem` shape required its dual: `newarr` of any WELL-FORMED
encoded element type (the type reference must resolve in a known scope —
malformed operands still withhold, per the tamper pin). The array identity
is tracked with exact slots; unstored elements stay unknown. The struct-array
fixture pins the pair end to end.

## Indexed For battery (2026-11-01, IL 0.2.56)

The first Opus-sourced pre-admission batch, landed while the scan runs:
- `sub`/`sub.ovf`/`sub.ovf.un` on ints, plus the `array-length - k` bound
  (the `For i = 0 To arr.Length - 1` lowering) with `conv.i4` passthrough
- the complete `.s`/`.un` comparison-branch family (`ble.s`, `blt.un.s`, …):
  array/length-minus-bounded backward loops unroll concretely with count-
  exact receipts; backward int-int counted loops (the get_Count bound)
  unroll as `index-bounded;count-exact`; forward int-int resolves
- `ArrayList.get_Count` (exact tracked count) and `get_Item(Int32)`
  (in-range exact slot reads)
- `Information.IsNothing` (tracked allocations/strings/boxed value types
  provably false; typed inputs and unbound strings fork nullness) and
  `Information.IsDBNull` (exact DBNull true; unbound values fork)
The indexed fixture (typed-array For + ArrayList For, both count-exact)
pins the family end to end.

## Field addresses (2026-11-01, IL 0.2.57)

v36-001 (and its earlier 0.2.55 sighting): `ldflda` — the address of a
field on a tracked holder (the ByRef struct-argument copy-back shape).
Admitted with the local-address discipline: owner-proved field addresses
carry (allocation, identity); Deref reads the field slot; stind.ref through
one writes the field honoring the frozen/mutation rules; addresses never
escape to the heap, framework arguments, or returns. The holder fixture
mutates through a ByRef helper and completes.

## ByRef forwarding across frames (2026-11-01, IL 0.2.58)

v37-001: lifecycle-address-frame-unavailable — the ByRef forwarding chain
(the 2-arg ExecProc overload passing its OWN locals' addresses into the
3-arg overload, which also writes through the root's forwarded address).
ByRef admission now accepts local addresses of ANY live ancestor frame
(dereference validates liveness) and owner-proved field addresses; the
overload-chain fixture (root -> Forward -> Run, stores through both array
addresses) completes. Any residual stale-address refusal carries an
addressFailure diagnostic (address identity + in-flight call), closing the
last anonymous stop family.

## Field-address encoding fix (2026-11-01, IL 0.2.59)

v38-001's addressFailure named the bug precisely: every field address on a
heap allocation self-corrupted — the allocation id ("allocation:N")
contains the ':' the parser split on, so Deref/stind misparsed every field
address as a frame lookup and threw address-frame-unavailable. The
separator is now '#' (unambiguous in both parts); ldind.ref dereferences
BOTH address kinds (local and field) instead of only locals. The
this-field ByRef chain (the `_lastcommand` shape: a field address on the
instance passed ByRef into a tracked callee that reads and stores through
it) is pinned in the holder fixture.

## Parameter read-backs (2026-11-01, IL 0.2.60)

v39-001: SqlParameter.get_ParameterName() on a tracked/attached parameter.
Admitted with its family: the constructor-proven name (exact string), the
collection get_Item(Int32) at an exact in-range index returning the
attached parameter identity, and get_Value as the STORED VALUE ORIGIN
(input/opaque/computed/dbnull — no runtime-value claim), opening output
copy-back reads. The DBNull constant joins the provably-non-null kinds.
Pinned in the forwarding fixture's read-back block.

## Both-null fold (2026-11-01, IL 0.2.61)

v40-001 outcome B2: `lifecycle-comparison-unresolved` with
`found:"null:null"` — the reference non-null test (`cgt.un`/`clt.un`
against `ldnull`) where the TESTED value is itself proven null on the
current fork path. The stop site reported since v28-001 (offset 64) is the
2-arg overload's forward-call site; `inFlightCall` wraps the whole inlined
callee window, so each round's stop was a different prologue guard inside
the 3-arg overload, peeled one operand family per batch (connection
allocation, forwarded array, and now the null-origin array). The fold:
both operands proven null resolves the test to the constant 0 (the is-null
side) — a concrete fact, never a fork — and the branch consumes it
concretely. `ceq` already folded both-null via Equal.

Two lowerings, two pins:
- the fixture twin (Debug lowerings: `ceq` + stored temp) pins the
  already-working `Equal(null, null)` corner in the forwarded-null shape;
- the Cecil-rewrite pin converts every `…; ldnull; ceq; stloc; ldloc; br*`
  null test into the Release-era .NET 1.x-vbc shape (`cgt.un`, temp
  nopped, branch polarity flipped) and walks it — without the fold that
  walk refuses with the v40 shape, with it both fork paths complete (the
  null path copy-backs an empty array through the forwarded address).

Version-label repair in this batch: the extractor/policy constants had
stayed at 0.2.56/v57 through the read-back batches while the recorded
sequence (and the prompts) cited 0.2.57-0.2.60/v58-v61; the commit pins
were always the real anchors. The constants now continue the recorded
sequence (0.2.61/v62).

## Walk closed — InfoMessage unmatched unsubscribe recorded (2026-11-02, v41-001)

v41-001 outcome B2 at commit 4da29cf942 ends the walk, as the final prompt
declared it would. The stop: `lifecycle-info-unmatched-unsubscribe` — an
InfoMessage unsubscribe (`remove_InfoMessage`) whose handler is not in the
tracked per-connection subscription set. Three honest causes fit the
refusal: the subscribe happened outside the walked window, on a different
connection identity, or in a handler form the boundary does not track as an
info-delegate. Admitting the shape is one evidenced line if a future pass
ever needs it; the walk does not get one.

The run itself is the strongest progress signal of the arc: the both-null
fold unblocked the 3-arg overload's guard chain, the walk did roughly twice
the lifecycle work of any prior round (680 progress events vs ~300), and
descended into InfoMessage-boundary territory for the first time — then
stopped there, under the opaque-entry schema, with a 19-member retained
slice and no candidate outcome. The chain of stops the walk never
reached (unbound sizes, terminals, the queued accumulator shapes) remains
open territory recorded in the declared boundaries, not evidence gaps.

Walk totals: 36 public batches since the takeover began (v12-001 through
v41-001), every stop family self-describing, every admission fixture-pinned,
both languages, both readers agreeing. The next deliverable is the
conversion-sufficiency contract: the origin table (exact / input /
opaque-initializer / computed-string / computed-int / dbnull / unbound)
and the one-method gated conversion attempt it feeds — two attempts
maximum, per the standing directive.

## External-detach unsubscribe (2026-11-02, IL 0.2.62) — the walk reopens

The owner reopened the walk after the v41-001 close: the conversion attempt
needs the private roots' real candidates, so the loop continues while the
next stops stay bounded. v41-001's stop — `lifecycle-info-unmatched-
unsubscribe` — is admitted as the external-detach shape: an unsubscribe of
a handler that passed every tracked-shape check (info-delegate, custom
owner, known fields) but was never subscribed on THIS connection inside
the walked window. The detach executes concretely — later fills on that
connection will not deliver to the handler — and the receipt records
`unsubscribe-external` so the evidence distinguishes an in-window
unsubscribe from an externally-attached one; the external subscription
itself stays unproven, exactly as the standing callback limitation already
declares. The InfoMessage policy moves to /5.

Pinned by a fixture twin (both languages): the matched `RemoveHandler`
followed by a second one — the second records `unsubscribe-external` and
the walk completes. A handler that fails the tracked-shape checks keeps
refusing.

## Outcome A — the walk completes (2026-11-02, v42-001, IL 0.2.62)

Both lifecycle roots completed and produced entry candidates under
`il-sql-lifecycle-state-alternatives/1` with `stateControlPolicy:
explicit-sql-state-control/1` (guards forked into bounded alternatives).
The 19-member retained slice finished with `dual-reader-agreed` retention
on every member, all four gap-failure properties null, 68,632 facts,
~844 MiB peak sampled working set (~1.16 GiB committed), 30.8 s. The
completing walk ran at normal progress-event volume (308) after v41's
failure-churn peak (680) — the external-detach admission removed the last
wall on the path. This is the walk's terminal success: entry candidates,
not SQL acceptance, not row proof. The candidates are the input for the
conversion-sufficiency contract (the origin table) and the one-method
gated conversion attempt.

## The conversion attempt — the arc's answer (2026-11-02)

The origin contract drove a real conversion of one walked root (a
four-argument DataSet-returning producer) in two attempts, per the
owner's two-attempt rule. Attempt 1: every scan-proven value generated
without a TODO — command text, four parameter names/types/sizes, the
whole guard/attach/execute structure — with 15 TODOs across 8 origin
kinds sitting exactly on runtime unknowns, and one honest finding (the
catch body carried no origin label). Attempt 2's delta against attempt 1
was exactly the two contract rows attempt 1 exposed (connection-state was
used but unlisted; exceptional-path had no row) — the contract, not the
model's judgment, constrained the change. The owner's Beyond Compare
review against the original source found zero semantic differences: the
scan-proven middle (SQL literal, four parameter constructor calls)
diffed clean; the catch transcribed character-exact; the only deltas
were a cosmetic concatenation-operator rendering and signature
formatting. Sufficiency verdict: the walk's evidence was sufficient for
what it labels, with the remaining TODOs documented as the proof's
limits — two attempts, one contract paragraph, measured.

## Review-fix batch (2026-11-02, IL 0.2.63 / v64)

PR review findings, fixed and pinned:
- **Strict integer comparisons** (Codex P2): the value form of `cgt`
  computed `!(x<y)`, so EQUAL operands folded true — a branch consuming
  it could select the wrong path with gap-free evidence. Each opcode now
  evaluates its own strict relation (`cgt` is `x>y`; the `.un` forms
  compare the unsigned reinterpretation), in both the int-int and
  int/array-length cases. Pinned by the stored-flag fixture: equal
  count-derived ints fold false and the entry completes (the pre-fix
  walk took the empty early return).
- **The Fill count stays unbound** (Codex P2): `Fill` fabricated `Int(0)`
  as its return. It now returns an unbound integer carrying the
  execution provenance (the CInt design): discarding is fine, a stored
  `> 0` flag forks the rows/no-rows hypothetical (pinned), and an
  equality test refuses by name
  (`lifecycle-equality-unresolved:computed-int:int`, negative pin).
- **Compile-only smoke** (baz): the bounded-input replay now passes
  `--compiled-only` and verifies `SourceAnalysisExcludedByCompiledOnly`
  is recorded, so source-only edits can no longer shift the hashed
  inputs; matrix schema moves to /4.
- **Lifecycle markdown provenance** (baz): the comparison report header
  carries repo, revision, rule, tier and evidence version; each row
  carries its source rule and tier (pinned through the matrix command's
  report.md).

## Review-fix batch 2 (2026-11-02, IL 0.2.64 / v65)

Round two findings, fixed and pinned:
- **Merges of written sources refuse** (Codex P2): merging a table that
  carries application-written columns discarded those origins — the
  merged table would report SQL origins for application-written data.
  `lifecycle-merge-writes-unmodeled` refuses by name (C#-only fixture:
  the shared native body budget sits at its pinned edge for the VB
  assemblies — VB fixture trees are unchanged this round).
- **Executed output parameters read back unknown** (Codex P2): an Output
  or InputOutput parameter's value after execution is a database result;
  the pre-execution origin is stale. C#'s ToString demand on the unknown
  refuses; VB's Conversions.ToString keeps it unbound and the comparison
  forks — never a concrete comparison against the initial value.
- **Boxed booleans retain their runtime type** (Codex P2): boxing a
  concrete Boolean kept the int representation, so `GetType().ToString()`
  rendered System.Int32 and the .NET 1 type-name dispatch took the wrong
  branch concretely. The boxing instruction's type is retained.
- **Literal comparisons compare by digest** (found while pinning the
  above): body evidence stores every ldstr literal as a digest; the
  static-type collapse derives type names as plain text; the ordinal
  shortcut ran ACROSS the two forms and could never match — the ctor's
  type-name dispatch has been silently folding not-equal since the
  collapse batch. A derived exact string now compares against a literal
  by reconstructing its own digest (equality decidable without the
  literal's text; the shortcut never crosses forms). This activated the
  per-string-input emptiness fork that mis-fold had skipped: fork counts
  double where that dimension is genuine, and the four-input full-root
  mirror now exceeds the 128-path capacity — it pins the path-limit
  refusal with the census naming the genuine families (the two-string
  production root shape still fits; a capacity raise is the owner's
  census decision, as at v30). Round-2 pins are C#-only by budget.

## Review-fix batch 3 (2026-11-02, IL 0.2.65 / v66)

Round three findings, fixed and pinned (C#-only pins; the VB sample
assemblies remain at their budget-pinned size):
- **Short unsigned branches honor unsigned ordering** (Codex P2): the
  `.un` marker was detected with an end-check that misses the `.s`
  suffix (`bgt.un.s` evaluated signed — the MaxValue bit pattern folded
  through the wrong side). The pin walks a `uint.MaxValue > 1u` branch
  that a signed misread skips.
- **`IsNothing` keeps its polarity** (Codex P2): the null-equality
  hypothetical was negated inside the shared helper, making `IsNothing`
  behave as `IsNot Nothing` for unbound inputs — every nullness
  assumption's side was swapped. The raw test is returned; no existing
  pin had ever asserted the (swapped) sides, which is why it survived.
- **Same-form plain strings compare by text** (Codex P2, introduced in
  batch 2 and caught one round later): two derived plain strings with
  identical text folded unequal. The same-form branch now compares
  texts (identical literal digests included).
- **Checked arithmetic refuses on concrete overflow** (Codex P2):
  `add.ovf`/`sub.ovf`/`.un` variants wrapped silently; a concrete
  overflow now refuses as `lifecycle-arithmetic-overflow` — the wrapped
  continuation is an impossible normal-completion path, never a
  successful lifecycle. The non-overflowing checked path keeps walking
  (pinned).

## Review-fix batch 4 (2026-11-02, IL 0.2.66 / v67)

Round four findings, fixed and pinned (C#-only pins; VB sample
assemblies remain budget-frozen):
- **Unbound-integer comparison polarity** (Codex P2, deferred from round
  1's notes): `cgt(x, 0)` pushed the raw equal-assumption — every strict
  relation against a 0/1 constant implies non-equality, so the
  expression's truth is always the negation; the zero-assumed
  alternative now completes and the rows-assumed alternative terminates
  exceptionally (pinned with observable sides).
- **Unequal literal comparisons keep equality-only semantics** (Codex
  P2): a digest mismatch establishes inequality, never an ordering. The
  result is the equality-only `int-nonzero` marker: equality consumers,
  the nonzero test and beq/bne.un resolve; ordering consumers refuse by
  name (pinned).
- **Array casts validate the element type** (Codex P2): newarr and
  ToArray snapshots carry their element type; a castclass to any other
  array type — which throws at runtime even empty — refuses as
  `lifecycle-cast-array-type`. The ldtoken (`tok:type:0x…:`) and cast
  (`t:`) encodings compare bare scoped-type tails (pinned).

## Review-fix batch 5 (2026-11-03, IL 0.2.67 / v68) — the review loop closes

Round five findings, fixed and pinned; after this batch the loop on this
PR ends and any further findings become follow-up issues:
- **Digest-only operands keep concatenations unbound** (Codex P2): a
  literal's digest Text is an encoding — folding it into an exact string
  fabricates a text no runtime value ever had. All-constant concatenation
  remains exact only when every operand is a plain-derived string (the
  array overload the same); anything else stays unbound (pinned: the
  equality consumer forks).
- **ToArray validates its elements** (Codex P2): Array.Copy throws while
  copying an incompatible element, so the snapshot is only creatable when
  every slot is provably the requested type — a tracked custom of that
  exact type, an exact string for String, an int for Int32. Anything
  else refuses as `lifecycle-toarray-element` (pinned).
- **The equality-only marker resolves solely against zero** (Codex P2):
  a digest mismatch establishes one fact — the result differs from zero.
  Comparisons against one, other integers, or other markers are
  unresolved and refuse.

## Census-driven keying dedup and capacity (2026-11-03, IL 0.2.68 / v69)

The v43-001 post-merge verification stopped at the path fence with a
census naming `string-compare:site/0 x45` — one fork family keyed by
position. Two corrections:
- **Markers key by their carried provenance** (text = "provenance|constant"):
  the marker kind itself falls to "site" in the provenance table, which
  UNDER-keyed — independent values' comparisons shared one fork, making
  some true combinations unrepresentable. When the literal digest sits on
  the LEFT, the key names the other operand (a digest has no provenance);
  two different literals fold concrete (their digests differ, so
  inequality is compile-time knowledge).
- **Capacity 128→256 paths / 12→16 sites**: the two-string root shape
  carries ~8 genuine runtime binaries. The keying dedup (one fact, one
  key) halved the fork-count ripples the digest fix had doubled, and the
  four-input full-root mirror COMPLETES again — 217 alternatives (216
  entries + the open-guard termination), three per-input emptiness keys,
  zero site-keyed families. The private-shape fixture (two strings, two
  Int32s through the direction overload) completes with entry candidates
  and >=2 distinct emptiness keys. The nine-guard C# ManyGuards keeps the
  beyond-budget census pin (512 > 256); VB's eight guards sit exactly at
  capacity and complete (256 alternatives, pinned).
