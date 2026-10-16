# Base44 normalized query evidence v2 — issue #713

Extractor `base44-evidence/0.5.0` introduced `88mph.entity-query.v2`
descriptors. Extractor `base44-evidence/0.18.0` preserves that descriptor
schema and adds the bounded string-normalization admission below plus
reference-typed `limit`/`skip` pagination controls. V2 retains
v1's source-bound filter/list/deleteMany argument structure, exact UTF-16
spans, traversal bounds, value redaction, and typed gaps.

V2 corrects one v1 modeling error: a direct identifier, property access, or
element access used as an implicit filter value is complete static evidence for
a runtime-deferred value expression. The operand remains a redacted `reference`
plus its exact source span, with its eventual structure and interpretation
deferred to exact-SDK and runtime conformance.

Reference admission is recursive and intentionally non-executable. Identifier
roots, `this`, property chains, and element chains whose indexes are literals or
admitted references are accepted. Bounded zero-argument string-normalization
chains over an admitted reference, such as `code.trim().toUpperCase()`, are also
accepted as redacted runtime-deferred operands because the static query
semantics retain the same field/operator with an opaque runtime-deferred value.
These are admitted method-name spellings, not proof of a built-in string method,
a string receiver, purity, or a string result. A custom `trim()` returning an
object is still a redacted `reference`; consumers must not narrow its runtime
contract. Arbitrary
calls, construction, assignment, or other unsupported expression at any receiver
or index depth make the whole operand unresolved with
`query:operand_expression_unsupported`; an outer member-access node cannot hide
executable syntax such as `helper().id` or `values[makeKey()]`.

This does not infer the reference's type or value, turn a reference into an
implicit equality claim, or authorize a narrower database/runtime contract.
The replacement runtime must accept the SDK's open query-object behavior.
Root filter bindings, spreads, computed or duplicate filter keys, logical composition,
unsupported expressions, unknown operators, empty operator
objects, and traversal overflow remain unresolved. Numeric `limit`/`skip`
controls admit nonnegative literals and redacted references.

The version bump is intentional: v1 defined an implicit reference as
unresolved. Consumers must explicitly admit v2 and the exact reviewed producer
identity. For ShopGenie's current SDK 0.8.5 case, the executable SDK source
independently confirms that it serializes the complete query object unchanged;
future SDK identities must be checked separately. Static completeness remains
neither runtime proof nor release readiness.
