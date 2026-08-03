# Design

## Projection boundaries

The QueryDef reader first retains DAO output metadata. When a select QueryDef exposes zero fields, a quote/bracket/parenthesis-aware static SELECT projection supplies only unique output names and ordinals. Those declarations remain partial for source lineage and runtime result shape, but are admitted to the protected field-name catalog used by DLookup composition.

Inline SQL bindings expose independent static-lineage and runtime-value coverage. Local object dependencies and direct output fields may be complete even when a predicate calls a VBA function whose runtime value is unknown.

The protected VBA bundle supplies a deterministic procedure-name catalog. A unique function name maps to its existing procedure stable key. Ambiguous or missing procedures remain gaps. No VBA source is persisted in standard facts.

Surface filter expressions use record-source field scope; same-named controls are not implicit filter targets. Explicit control namespaces remain separately recognizable.

## Validation

- Query output fallback and coverage-separation unit tests.
- VBA procedure-catalog and expression-link unit tests.
- Scoped filter regression test.
- Focused Access and full solution tests.
- Same-snapshot private regeneration and deterministic remainder census.
