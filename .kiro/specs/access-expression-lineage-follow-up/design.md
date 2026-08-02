# Design

## Approach

Precompute stable identities for every control on a form or report before projecting bindings. Pass a case-insensitive map of control name to stable-key candidates into the bounded expression projector.

The expression projection retains separate collections for:

- record-source field candidates;
- same-surface control candidates;
- hash-only control references;
- hash-only Access session/surface namespace references;
- query/table candidates;
- domain selected-field candidates; and
- domain criteria-field candidates.

A unique same-surface control name produces an exact stable-key candidate. Multiple matching controls, or a name matching both a scoped field and a control, remain partial with `AccessBindingExpressionTargetAmbiguous`. Unknown session/global namespaces remain explicit unresolved evidence unless a bounded classifier recognizes their external-context shape.

A complete parented `query-field` catalog record may supplement query outputs missing from the base scan only when its parent query is already matched to the base scan. TraceMap derives the field stable key from the database seed, matched parent query, and protected identity, verifies any producer-declared key, and retains the catalog record as support. It never accepts a second query identity or an unparented field.

## Fact contract

`AccessBindingDeclared` under `legacy.access.binding.v1` gains the optional property `expressionControlStableKeys`. Existing properties remain stable. The rule limitation remains static declaration only, with no evaluation or runtime-target proof.

## Privacy and determinism

Only stable keys, role hashes, counts, classifications, and coverage labels persist. Raw expressions are hashed. Candidate collections are distinct and ordinal-sorted.

## Validation

- projector unit tests for exact control resolution, ambiguity, domain criteria, and dynamic expressions;
- UI projector/fact persistence tests;
- focused Access tests and full solution tests;
- protected representative enrichment and identity projection from a single provenance-compatible source snapshot;
- private before/after census;
- private-path and diff checks.
