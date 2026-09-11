# Base44 entity payload shape v2 — issue #713

Extractor `base44-evidence/0.8.0` emits payload `shapeVersion=2` with three
closed, deterministic properties in addition to the v1 field/spread evidence:

- `outerKind`: `object`, `array`, or `unknown`;
- `referenceAccounting`: `source-bounded` or `unresolved`;
- `runtimeObligationsJson`: a sorted, duplicate-free JSON string array whose
  only current token is
  `entity-open-object-fields:docker-write-readback-cleanup`.

The extractor emits `runtime-deferred-object-fields` only when executable
syntax proves an object-valued construction at the SDK boundary, the mutation
hook and its references are finitely accounted in the source file, and the
only missing information is the field set copied from a runtime-provided
spread. Mutation, escape, cross-scope, computed-key, conflicting-kind,
callsite, or binding-state uncertainty prevents that classification.

A deferred object remains `partial` and `Tier4Unknown`; it is not a complete
static shape. Its exact runtime obligation remains a release blocker unless a
consumer independently admits the exact SDK's open-object transport and later
proves an isolated unknown-field write, fresh readback, tenant isolation, and
verified cleanup in local Docker. Direct opaque parameters with no proven
outer object stay `outerKind=unknown`, `referenceAccounting=unresolved`, and
retain their original typed analysis gaps.

Extractor `base44-evidence/0.9.0` additionally carries outer-kind evidence
through conditional expressions, React Query callsites, array literals, and
finite `push`/`map` element paths. `completeness=complete` is forbidden when
`outerKind=unknown`; conflicting or missing outer-kind evidence emits the typed
`payload-outer-kind-unresolved` gap. Only direct `.push(...)` syntax on a
source-proven local array is modeled. Spread pushes, open elements, aliases,
escapes, conflicting element kinds, and other iterator methods remain
unresolved.

Object-rest projection is independently supported only for a final identifier
rest element with statically named excluded keys and a resolved source object.
The projection removes only those named fields. Source gaps, binding mutation,
and escape evidence are retained. Array destructuring, selected-property
bindings, and computed or default exclusions remain unresolved.

Packet validation rejects unknown enum values, malformed/duplicate obligation
tokens, orphaned obligations, extractor-0.8/0.9 payloads without shape v2, or a
deferred-object row that is not exactly object/source-bounded/partial/Tier4
with its required obligation.

The payload fact digest inputs remain the deterministic scan ID, fact/rule
type, source path and call lines, entity/operation identity, and every sorted
property including `outerKind`, `referenceAccounting`,
`runtimeObligationsJson`, `analysisGapsJson`, normalized fields/spreads, and
source-file SHA-256. Evidence additionally binds the SDK-call snippet SHA-256
and extractor identity/version. Runtime values are never included.

Extractor `base44-evidence/0.14.0` supersedes this producer format with
`shapeVersion=3` while retaining every v2 outer-kind and obligation property.
Version 3 adds a separately versioned semantic field projection; v2 remains
readable but is not semantic type-promotion authority. See
[Base44 entity payload shape v3](BASE44_ENTITY_PAYLOAD_SHAPE_V3.md).
