# Compiled SQL lifecycle capability map — public interpreter state (IL 0.2.27)

Public, sanitized capability map of `IlSqlLifecycleExtractor` and its opt-in
boundaries against the bounded compiled wrapper protocol, derived from the public
synthetic fixtures and interpreter source only. Inventory is not proof of runtime
reachability on any private body. Version `il-body-evidence/0.2.29`, evidence
policy `explicit-il-body-evidence.v30`, state control `explicit-sql-state-control/1`.

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
| Parameter objects | inspected custom fields/properties; frozen on ArrayList Add | mutation/escape after freeze refuses |
| Collection | exact net48 ArrayList: new/Add/GetEnumerator, one complete enumeration ≤16 | second enumeration, mutation, unsupported collections refuse |
| Attachment | `SqlCommand(name, connection)`, CommandType, `SqlParameter(name, type, size)`, `set_Value` (scalar origins), `Parameters.Add` | reuse after attachment refuses |
| Execution | `SqlDataAdapter.Fill` per command/dataset pair, execution identity per Fill | second Fill/merge into used shapes refuses |
| Results | returned `DataSet` allocation, stored dataset fields, table states, per-column origins | origin-less returned dataset refuses |
| InfoMessage | exact delegate ctor/subscribe/unsubscribe, callback inspection (names/naming lists), post-Fill naming receipts, framework 2/4 profiles | callback execution, sender access, list escape refuse |
| External trace | exact System.Web `TraceContext.Write` boundary incl. computed strings, stack-name chain, trace locals; the `StackTrace`/`GetFrame`/`GetMethod` name chain also resolves in non-callback lifecycle bodies under the flag (every step unbound, frame index 0-31) | arbitrary argument computation still bounded; without the flag the chain refuses |
| Runtime State | exact signed `SqlConnection/DbConnection.get_State` reads (InfoMessage opt-in): unbound observation + receipt | wrong key/version/shape refuses |
| Command config | `set_CommandTimeout`, `set_Transaction(null-only)`, `set_Direction` (pre-attachment), 4-arg source-column `SqlParameter` ctor, `TableMappings.Add` | tracked/mutated operands refuse |
| Typed arrays | `ArrayList.Clear` (pre-freeze), `ToArray(Type)` snapshot, exact-count indexed loops (`ldlen`/`ldelem.ref`/`conv.i4`/`add` + backward `blt`/`clt`+`brtrue` bounded by array length, C# and VB lowerings) | non-array comparisons, mutated-after-freeze refuse |
| VB scalar helpers | `Interaction.IIf` (concrete Boolean only), `Conversions.ToInteger` (Int32 only) | state-test conditions, non-Int32 operands refuse |
| Strings & reflection | `String.Concat` 2-4 args (compiler folds chained concatenation; exact for all-constant operands, unbound otherwise), `Object.GetType`, `MethodBase.GetCurrentMethod`, `MemberInfo.get_Name`, `Exception.get_Message`/`get_InnerException` (unbound strings, no runtime claim) | other uses refuse |
| Event log | `EventLog.WriteEntry(String,String)` static terminal under the external-trace boundary flag, trace-scalar args only | non-trace args, callback context refuse |
| Configuration | the `ConfigurationManager` chain in the root constructor becomes opaque field origins under `--il-opaque-root-initializers` (three-call chain receipt); no catalog entries needed | without the opt-in: unknown-call refusal |
| State control | comparison of one unbound State against a compiled constant forks bounded path alternatives (≤8 paths, ≤6 sites, per-alternative identities), including the Roslyn zero-constant lowering `State != <0-member>` → `ldc.i4.0 + cgt.un`; direct-throw construction + uncaught throw terminates its alternative; early leave abandons an active loop without entry evidence | state-vs-state, non-constant comparisons, caught throws, backward state branches, over-budget forks refuse |
| Control flow | forward branches; single enumerator loop with loop/collection tests; try/finally via leave/endfinally; ceq on concrete ints/null/fresh aliases; negation/identity lowering of one state comparison | unproven loops, loop-carried fields, comparison-branch family (beq/bne.un/bge/…) and relational ops refuse |

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

## Work-machine reconciliation still open

This map is derived from public fixtures only. The companion prompt
`prompts/inspect-sql-lifecycle-whole-protocol.md` remains the one bounded read-only
pass to reconcile this map against the two selected private inner roots (existing
compiled-only outputs; no scan). The single bounded ask it may produce: the exact
missing signed signatures/opcode shapes with offsets, never source, SQL or bodies.

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
| crypto-shaped transform without the compiled-scalar boundary | by-design: the boundary is the opt-in |
| stack-name chain without the external-trace boundary | by-design: the boundary is the opt-in |
