# Public compiled-wrapper SQL evidence

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
