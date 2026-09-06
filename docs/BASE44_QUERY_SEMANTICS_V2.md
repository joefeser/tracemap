# Base44 normalized query evidence v2 — issue #713

Extractor `base44-evidence/0.5.0` emits `88mph.entity-query.v2` descriptors.
It retains v1's source-bound filter/list/deleteMany argument structure, exact
UTF-16 spans, traversal bounds, value redaction, and typed gaps.

V2 corrects one v1 modeling error: a direct identifier, property access, or
element access used as an implicit filter value is complete static evidence for
a runtime-deferred value expression. The operand remains a redacted `reference`
plus its exact source span, with its eventual structure and interpretation
deferred to exact-SDK and runtime conformance.

This does not infer the reference's type or value, turn a reference into an
implicit equality claim, or authorize a narrower database/runtime contract.
The replacement runtime must accept the SDK's open query-object behavior.
Root filter bindings, spreads, computed or duplicate keys, logical composition,
unsupported expressions, dynamic controls, unknown operators, empty operator
objects, and traversal overflow remain unresolved.

The version bump is intentional: v1 defined an implicit reference as
unresolved. Consumers must explicitly admit v2 and the exact reviewed producer
identity. For ShopGenie's current SDK 0.8.5 case, the executable SDK source
independently confirms that it serializes the complete query object unchanged;
future SDK identities must be checked separately. Static completeness remains
neither runtime proof nor release readiness.
