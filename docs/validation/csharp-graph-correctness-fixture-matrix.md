# C# Graph-Correctness Adversarial Fixture Matrix

This matrix converts failure classes observed in Graphify issue, changelog, and
test history into independent TraceMap fixtures. It does not copy Graphify code.
Every implemented case uses synthetic source and deterministic Roslyn evidence.
The dangerous failure is a plausible graph that silently joins, drops, guesses,
or reverses evidence.

Issue [#591](https://github.com/joefeser/tracemap/issues/591) tracks the full
matrix. Implemented slices cover canonical identity, partial types, receiver
resolution, and full-snapshot stability. Direction persistence is being handled
as a separate bounded slice; incomplete legacy project inputs remain. Reverse
impact traversal belongs to #590 and is explicitly outside this matrix.

TraceMap currently rebuilds immutable full snapshots. It does not expose an
in-place incremental replacement API. `EvidenceSpan.FilePath` identifies where
evidence was observed; it is not entity ownership or replacement/deletion
authority. A referenced target location likewise grants no mutation authority.
If incremental replacement is introduced later, only an explicit
changed/deleted/excluded extraction-unit record may authorize pruning.

| ID | Category | Minimal source/project shape | Dangerous naive result | Required TraceMap contract | Status |
|---|---|---|---|---|---|
| ID-01 | Canonical identity | Two projects declare `Shared.Collision` and call same-named `Ping` methods | One label-based type or call hub spans both assemblies | Tier 1 type and method IDs include assembly identity; call endpoints remain in the declaring assembly; exact source spans and extractor provenance are retained | Implemented in `Same_name_types_keep_assembly_aware_ids_and_local_framework_collisions_separate` |
| ID-02 | Canonical identity | A source-defined `Task` and framework `System.Threading.Tasks.Task` are used in one caller | Local and external members collapse because their labels share `Task` | Canonical IDs and callee assembly provenance distinguish local `Run` from framework `GetAwaiter`; no gap is emitted for the compiling fixture | Implemented in the ID-01 test |
| ID-03 | Canonical identity | Same simple type name in different namespaces | Namespace qualification is discarded | IDs remain namespace-qualified | Planned follow-up |
| ID-04 | Canonical identity | Same method name with overloads | Overloads collapse into one endpoint | Method IDs retain parameter types and return type | Existing semantic coverage; dedicated adversarial fixture planned |
| ID-05 | Canonical identity | Nested types, aliases, and `using` aliases | Alias or nested label is treated as canonical identity | Roslyn-resolved original symbol supplies the endpoint ID | Planned follow-up |
| ID-06 | Canonical identity | Generic definition and multiple constructed types | Incompatible constructions collapse | Definition identity and type arguments remain explicit according to the canonical ID contract | Planned follow-up |
| ID-07 | Canonical identity | Source symbol and unresolved/external symbol share a label in one compilation | Unresolved label binds to a source declaration | No Tier 1 edge is guessed; retain Tier 3 syntax evidence and a Tier 4 gap where compilation proves missing identity | Planned follow-up; RX-04 covers unresolved fallback only, not this same-label collision |
| PT-01 | Partial types | `Worker` halves are in two source files; `Run` calls `Execute` from the other half | Files create two type nodes or the cross-file call disappears | Both declarations carry one canonical type ID; caller and callee endpoint IDs are Tier 1 and span the real call site | Implemented in `Partial_type_declarations_merge_and_cross_file_call_uses_canonical_member_endpoints` |
| PT-02 | Partial types | Both partial halves access one helper member | Member receiver differs by declaration file | Both call sites resolve to the same helper member ID with distinct evidence spans | Implemented in the PT-01 test |
| PT-03 | Partial types | Same partial type name exists in separate assemblies | Partial merging crosses an assembly boundary | Assembly-qualified type IDs remain distinct | Covered by ID-01 assembly boundary; dedicated partial-project fixture planned |
| PT-04 | Partial types | Nested partial type | Nested containing identity is lost | Canonical containing-symbol identity is preserved | Planned follow-up |
| PT-05 | Partial types | Generated and source partial combination | Generated half is silently treated as complete source coverage | Preserve source evidence and report reduced/generated coverage where supported | Planned follow-up |
| RX-01 | Receiver resolution | Calls use pairwise-distinct field, parameter, and property receiver types whose member names collide | All calls bind to the same label-matched receiver | Tier 1 target IDs follow each distinct Roslyn receiver type and retain caller/callee endpoint provenance | Implemented in `Receiver_resolution_honors_fields_parameters_inline_declarations_and_shadowing_and_reports_unresolved_calls` |
| RX-02 | Receiver resolution | `out var` and pattern variables use receiver types distinct from each other and from field, parameter, and property receivers while calling same-named members | Inline declarations are omitted or bound outside lexical scope | Each call resolves to its distinct inline variable type and exact call-site span | Implemented in the RX-01 test |
| RX-03 | Receiver resolution | A local shadows a field; `receiver` and `this.receiver` target different same-named types | Lexical rebinding is ignored | Local and explicitly qualified field calls have different canonical target IDs | Implemented in the RX-01 test |
| RX-04 | Receiver resolution | A missing receiver type is invoked in an otherwise extractable file | Analyzer invents a Tier 1 target or reports a clean graph | No Tier 1 call exists at the unresolved site; Tier 3 syntax call remains; CS0246 is a Tier 4 `AnalysisGap` with line and extractor provenance | Implemented in the RX-01 test |
| RX-05 | Receiver resolution | Static, extension, and interface-typed calls | Dispatch shape or reduced extension identity is mislabeled | Preserve compiler-selected method identity and declared evidence limitations | Planned follow-up |
| DIR-01 | Directionality | Caller/callee, project-reference, inheritance, route/service, and supported boundary edges survive persistence | A reversed edge produces plausible but incorrect paths | Serialized and loaded source/target IDs exactly preserve each rule's declared direction | Planned later slice; no reverse traversal in this work |
| SNAP-01 | Full-snapshot stability | Full scan, caller-only edit, file move, target deletion, and scoped exclusion followed by complete snapshot rebuilds | A rebuilt snapshot drops unchanged evidence, retains old paths/semantic edges, or analyzes a file excluded from inventory | Canonical endpoints survive caller edits/moves; old paths disappear; target deletion degrades to syntax plus a Tier 4 gap; scoped exclusions are recorded and cannot emit or influence semantic facts; unrelated SQL evidence remains stable | Implemented in `CSharpFullSnapshotStabilityTests`; in-place incremental replacement remains unsupported and unclaimed |
| LEG-01 | Legacy/incomplete .NET | Legacy project, unavailable reference, outside-root project, or conditional compilation fails full load | Failed compilation becomes a clean empty graph | Preserve extractable syntax/structural evidence, emit explicit gaps, and label coverage reduced | Planned later slice |

## Assertion contract

Implemented call-edge fixtures assert exact canonical `sourceSymbolId` and
`targetSymbolId` values from semantic call facts, caller/callee assembly name
and version, rule ID, evidence tier, normalized file path, exact line span,
extractor ID, and extractor version. Each synthetic repository is committed
before scanning, and its manifest and facts must retain that 40-character SHA.
Assertions run against `facts.ndjson` readback; focused SQLite checks also prove
that semantic call endpoints, provenance, spans, tiers, and gap properties
survive `index.sqlite` persistence. TraceMap does not currently emit independent
Tier 1 `MethodDeclared` facts, so these tests claim canonical call-fact identity,
not independent method-declaration evidence. Negative cases assert both the
missing Tier 1 relationship and the retained Tier 3/Tier 4 evidence. Aggregate
counts alone are not considered graph-correctness proof.
